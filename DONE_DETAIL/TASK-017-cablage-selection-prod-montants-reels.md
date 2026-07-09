# TASK-017 — Câblage sélection prod réelle + montants réels dans le figeage API (avenant TASK-012)

## Contexte
TASK-012 a livré l'API TVA (workflow, persistance, transparence, checkup, clôture) **sur fixtures**
(`FixtureSelectionExpliqueeService`, injecté en Development). Pour passer à un **test réel bout-en-bout**
sur la base prod `GR_EMA_DISTRIBUTION` (`SO_Id=1`, accès validé TASK-001), trois câblages sont manquants —
ce sont des **branchements**, pas un manque de credentials :

1. **`DbConnectionFactory` est un stub** : [`CreateGrfConnection`/`CreateSageConnection`/`CreatePersistenceConnection`]
   renvoient tous `SqliteConnection("Data Source=tva.db")` en dur et ignorent `IConfiguration`
   (`Declaration.Infrastructure/Factories/DbConnectionFactory.cs`).
2. **Chaîne de connexion non transmise** : `DeclarationWorkflowService.ChargerCandidatesSiNecessaireAsync`
   passe la chaîne littérale `"connection_injected_by_di"` à `SelectionnerExpliqueeAsync`
   (`Declaration.Application/Services/DeclarationWorkflowService.cs:65`). En prod,
   `SelectionExpliqueeService` ouvre `new SqlConnection(connectionString)` → échec `OpenAsync`.
3. **Montants déclaratifs fabriqués** : HT/Taux/TVA/TTC sont codés en dur (`Taux=20m`,
   `TVA=MontantAffecte*0.2`, `TTC=*1.2`) dans le mapping du figeage (mêmes lignes ~82-84), appliqués
   aussi bien aux fixtures qu'aux données réelles. Les vraies taxes proviennent du worker OM +
   ventilation (`Declaration.Core` via l'orchestration TASK-007), non branchés dans ce chemin.

## Périmètre STRICT
- **Uniquement** : le câblage prod du chemin de **figeage des candidates** de l'API
  (`ChargerCandidatesSiNecessaireAsync`) + la configuration réelle des connexions.
- **Exclu** : la logique de sélection SQL (déjà TASK-008/015), le calcul de ventilation (déjà
  `Declaration.Core`/TASK-004/006), l'orchestration worker (déjà TASK-007). On **branche** l'existant,
  on ne le réimplémente pas. Génération (TASK-010/011) et contrôle (TASK-009) restent hors périmètre.

## Étapes
### Étape A — Sélection prod réelle (débloque le test transparence sur données réelles)
1. **`DbConnectionFactory` lit la config** : chaînes `GrfConnection` (SQL Server prod), `SageConnection`,
   `PersistenceConnection` depuis `appsettings.json` / `appsettings.Production.json` — **multi-client**,
   jamais en dur. Conserver SQLite uniquement pour la persistance module en dev si voulu.
2. **Exposer la chaîne GRF** au workflow : `SelectionExpliqueeService` attend une `string`, la factory
   renvoie un `IDbConnection` → ajouter un accès à la **chaîne** GRF (ex. `GetGrfConnectionString()`
   sur `IDbConnectionFactory`, ou lecture `IConfiguration` dans le service). Décision d'archi à trancher
   au démarrage (préférence : méthode sur la factory, cohérente avec le multi-client).
3. **Passer la vraie chaîne** dans `ChargerCandidatesSiNecessaireAsync` au lieu du placeholder.
4. **Bascule d'environnement** : Production → `SelectionExpliqueeService` (déjà câblé en DI, Program.cs:40).

> À l'issue de l'Étape A : `GET /declarations/{id}/lignes` renvoie les **vraies** affectations
> candidates de la prod (éligibles **et** écartées avec motif réel) → **test transparence réel possible**.

### Étape B — Montants réels
5. **Brancher l'orchestration OM/ventilation** (TASK-007) dans le figeage : remplacer HT/Taux/TVA/TTC
   forfaitaires par les taxes réelles issues du worker OM out-of-process + ventilation `Declaration.Core`.
6. Gérer les factures introuvables / taxe non à taux → **motif d'écartement** (pas de silence), cohérent
   avec la règle n°1 de transparence.

## Contraintes techniques
- **Aucune écriture** hors store module (`DeclarationEntete`/`LigneCandidate`) — isolation GRFN intacte.
- **Multi-client** : connexions et paramètres en configuration, jamais en dur.
- **Worker OM out-of-process** (x86) invoqué via l'orchestration existante ; l'API reste x64/AnyCPU.
- Ne rien changer au contrat d'API public (mêmes endpoints/DTO qu'en TASK-012).

## Livrables
- `DbConnectionFactory` réelle (config multi-client) + workflow passant la vraie chaîne GRF.
- Figeage produisant des lignes réelles ; Étape B : montants réels via orchestration.
- `VERIFY/TASK-017_verify.md` : **captures réelles sur `GR_EMA_DISTRIBUTION`** (login → création →
  `GET /lignes` avec vraies candidates éligibles + écartées-avec-motif → checkup → clôture), + preuve
  d'isolation (aucune écriture `RT_*`), + comparaison montants figés vs source pour un échantillon.

## Critères de validation
- `GET /lignes` renvoie des candidates **réelles** de la prod (pas la fixture), transparence conservée
  (écartées + motif visibles).
- Étape B : HT/Taux/TVA/TTC = valeurs **réelles** (worker OM/ventilation), plus de forfait 20 %.
- Aucune écriture `RT_*` ; connexions issues de la config (aucune chaîne en dur).
- Le chemin fixture (Development) reste fonctionnel pour les tests hors base.

## Risques / dépendances
- **Prérequis** : TASK-007 (orchestration), TASK-008/015 (sélection), accès prod TASK-001 (acquis).
- **⚠️ Dépendance partagée avec TASK-009** : les deux tâches ont besoin d'une **`DbConnectionFactory`
  réelle** (connexions prod issues de la config). Faire l'**Étape A en premier** (ou l'attribuer à une
  seule tâche) pour éviter que TASK-009 et TASK-017 modifient `DbConnectionFactory`/`Program.cs` en
  parallèle. Une fois l'Étape A posée, TASK-009 et l'Étape B peuvent avancer en parallèle sans conflit.
- **Interface mismatch** `IDbConnection` vs `string` de connexion → petit choix d'archi (Étape A.2).
- Worker OM x86 : dépendance runtime (COM Sage) — l'Étape B nécessite l'environnement worker opérationnel.
