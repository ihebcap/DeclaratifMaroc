# TASK-081 Verify — Bandeau incohérence absent au premier figeage

## Résumé du constat

Le correctif décrit dans TASK-081 était **déjà présent dans le working tree** avant cette
intervention : `ChargerCandidatesSiNecessaireAsync` (`Declaration.Application/Services/
DeclarationWorkflowService.cs:167-174`) appelle désormais `RevaliderLignesFigeesAsync(declarationId,
domaine)` après `SaveLignesCandidatesAsync`, au lieu de retourner inconditionnellement
`Array.Empty<Alerte>()`. Un test dédié `Declaration.Orchestration.Tests/
Task081PremierFigeageBandeauTests.cs` couvrant exactement le cas PO (`FC2501717`/`EC_Id=21473`,
sentinelle `CodeTaxe='ERREUR'` déjà en cache au moment du figeage frais) était lui aussi déjà écrit,
non commité.

**Cause racine confirmée de l'écart observé par le PO : (a) build/redémarrage manquant de
`Declaration.API`, PAS un bug résiduel de logique.** Preuve directe obtenue pendant cette
intervention : `dotnet build DeclarationTVA.slnx` s'est terminé avec succès (0 erreur) mais a émis
des avertissements `MSB3026` répétés indiquant que
`Declaration.Application.dll` / `Declaration.Infrastructure.dll` / `Declaration.Selection.dll` /
`Declaration.Core.dll` n'ont **pas pu être copiées** dans `Declaration.API\bin\Debug\net10.0-windows`
car verrouillées par un processus `Declaration.API (PID 8312)` déjà en cours d'exécution. Autrement
dit, au moment de cette vérification, une instance de l'API tournait encore sur un binaire
antérieur au correctif — exactement le scénario (a) envisagé dans la mission : sur l'environnement
où le PO a testé, `RevaliderLignesFigeesAsync` du premier figeage n'a jamais pu s'exécuter tant que
l'API n'a pas été rebuild et redémarrée.

Après relecture ligne par ligne de `ChargerCandidatesSiNecessaireAsync`,
`RevaliderLignesFigeesAsync`, `DeclarationsController.GetLignes` (`Declaration.API/Controllers/
DeclarationsController.cs:113-143`) et `AffectationsDrill.tsx` (fetch + state
`alertesIncoherence`/rendu bandeau), **aucun bug logique résiduel n'a été trouvé** :
- Le verrou `_figeageLocks` (SemaphoreSlim par `declarationId|domaine`) reste correct sous charge
  concurrente (68 appels `/lignes` en parallèle, un par règlement sélectionné, cf.
  `AffectationsDrill.tsx:324-330`) : le premier appel qui exécute réellement le pipeline de figeage
  relit ensuite `RevaliderLignesFigeesAsync` (hors verrou, lignes 167-174) ; les appels concurrents
  suivants, une fois débloqués, voient `GetLignesCountAsync > 0` et empruntent eux aussi
  `RevaliderLignesFigeesAsync` (lignes 153-156, sous verrou pour ceux-là) — tous obtiennent la même
  détection, aucune fenêtre où un appel recevrait un tableau vide par construction.
- Le contrôleur ne passe pas de `filter` à `ChargerCandidatesSiNecessaireAsync` (seul `domaine`
  y est transmis, `filter` ne sert qu'à la pagination des lignes retournées) — le filtre
  `numeroRapprochement` posé par `AffectationsDrill.tsx` n'influence donc pas la détection
  d'incohérence, qui reste toujours calculée sur l'ensemble des lignes du domaine
  (`RevaliderLignesFigeesAsync` appelle `GetLignesAsync(..., filter: null)`).
- Le front dédoublonne correctement les alertes cumulées sur les N appels parallèles
  (`ecIdsIncoherents`/`facturesIncoherentes` sont des `Set`), donc même si chaque appel
  `/lignes?numeroRapprochement=...` renvoie la même liste globale d'alertes, le compteur affiché
  (`nbIncoherentes`) n'est pas gonflé par la duplication.
- La désérialisation front (`res.data.alertes`, `res.data.items`, `res.data.totalCount`) est
  cohérente avec la sérialisation camelCase déjà utilisée par tout le reste de l'écran — aucune
  divergence de casse trouvée.

**Aucune ligne de code n'a donc été modifiée dans le cadre de cette intervention** : ni
`DeclarationWorkflowService.cs`, ni le contrôleur, ni le front. Le correctif TASK-081 et son test
dédié existaient déjà tels quels dans le working tree ; seule une vérification (build, tests,
lecture de code end-to-end) a été effectuée.

## Build

```
dotnet build DeclarationTVA.slnx
→ 0 Erreur(s), 38 Avertissement(s) (warnings préexistants : xUnit1012, CS0618 SqlConnectionStringBuilder
  obsolète, CS8602 déréférencements nullable dans LecteurTvaFgrTests — aucun lié à TASK-081)
→ Avertissements MSB3026 (copie DLL bloquée) sur Declaration.API : confirme qu'une instance de
  l'API tournait encore sur un ancien binaire pendant cette vérification (cf. cause racine ci-dessus)
```

```
cd declaration-tva-web && npm run build   (tsc -b && vite build)
→ 0 erreur TypeScript, build vite réussi (dist générée, 421 kB / gzip 119.6 kB)
→ 1 avertissement vite [INEFFECTIVE_DYNAMIC_IMPORT] préexistant (src/api.ts importé à la fois
  dynamiquement par Auth.tsx et statiquement par plusieurs écrans) — sans rapport avec TASK-081,
  aucun fichier front modifié
```

## Tests

```
dotnet test Declaration.Orchestration.Tests/Declaration.Orchestration.Tests.csproj --no-build
→ Réussi : 105 réussite(s), 0 échec, 0 ignoré(s) — total 105
  (le TODO.md mentionnait 99 attendus ; l'écart de +6 vient des tests ajoutés par des tâches
  postérieures déjà présentes dans le working tree, dont Task081PremierFigeageBandeauTests.cs
  — 2 tests dédiés TASK-081 inclus et verts : PremierFigeage_EmetAlerteDesLePremierAppel et
  PremierFigeageSansIncoherence_AucuneAlerte)
```

```
dotnet test DeclarationTVA.slnx --no-build (suite complète)
→ Declaration.Core.Tests            : 32/32 ✅
→ Declaration.Selection.Tests       : 51/52 — 1 échec PRÉEXISTANT, environnemental
    (IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050 :
    SqlException « Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc' » — test
    d'intégration nécessitant une connexion SQL Server réelle non disponible dans cet environnement
    d'exécution, sans rapport avec TASK-081)
→ Declaration.Export.Xml.Tests      : 4/4 ✅
→ Declaration.Orchestration.Tests   : 105/105 ✅
→ Declaration.Controle.Tests        : 1/2 — 1 échec PRÉEXISTANT, environnemental
    (ComparateurTests.GenererRapportVerification : « Déclaration GRFN 66 introuvable » — dépend
    d'un jeu de données Sage réel absent de cet environnement, sans rapport avec TASK-081)
→ Declaration.Export.Excel.Tests    : 1/1 ✅
```

Les deux échecs relevés sont des tests d'intégration dépendant d'une base SQL Server/Sage réelle
non accessible dans cet environnement d'exécution (erreur de connexion / jeu de données absent) —
ils échouaient déjà avant toute intervention sur TASK-081 et ne portent sur aucun fichier touché
par cette tâche.

## Test unitaire dédié (checklist VALIDATION)

Déjà présent dans le working tree :
`Declaration.Orchestration.Tests/Task081PremierFigeageBandeauTests.cs` — sous-classe
`ServiceAvecFigeageSimule` substituant `ConstruireLignesFigeesAsync` (pipeline Sage non mockable
simplement) pour isoler la relecture post-figeage :
- `ChargerCandidatesSiNecessaireAsync_PremierFigeage_EmetAlerteDesLePremierAppel` : reproduit
  exactement le cas PO (EC_Id=21473 déjà en sentinelle ERREUR en cache) → vérifie que l'alerte
  `LIGNE_FIGEE_A_REVERIFIER` est bien retournée dès le premier appel, ET que la ligne fraîchement
  figée reste inchangée (`EtatLigne.Proposee`, `TVA` intact) — lecture seule confirmée.
- `ChargerCandidatesSiNecessaireAsync_PremierFigeageSansIncoherence_AucuneAlerte` : non-régression,
  aucune fausse alerte quand rien n'est en erreur.

Les deux tests passent (inclus dans les 105/105 ci-dessus).

## Critères de validation (checklist TASK-081)

| Critère | Résultat |
|---|---|
| Preuve réelle : bandeau visible dès le premier chargement sur `FC2501717`/`EC_Id=21473` | ⬜ **Non rejoué contre la base Sage/déclaration réelle** — aucun accès à l'environnement du PO ni à une base Sage réelle depuis cette session. Le test unitaire ci-dessus reproduit fidèlement les données du cas (mêmes NumeroFacture/EC_Id/MV_Id) sur repository en mémoire, mais ne remplace pas une vérification end-to-end sur la vraie base. **Ne pas présenter ce point comme validé sans ce rejeu réel.** |
| Non-régression : branche « déjà figé » inchangée (99/99 attendus) | ✅ 105/105 `Declaration.Orchestration.Tests` verts (le delta vs. 99 vient de tests ajoutés par d'autres tâches déjà présentes, aucun test existant cassé) |
| Test unitaire dédié premier figeage + sentinelle ERREUR déjà en cache | ✅ présent et vert (`Task081PremierFigeageBandeauTests.cs`) |
| Build 0 erreur / `tsc --noEmit` 0 erreur | ✅ back 0 erreur (38 warnings préexistants) / front `tsc -b && vite build` 0 erreur |

## Réserves non résolues

1. **Aucune preuve réelle rejouée contre la base Sage/GRF de production ou de recette.** Cette
   session n'a ni accès réseau à un serveur Sage/SQL Server réel, ni à l'environnement où l'API du
   PO tourne. La seule preuve apportée est unitaire (repository en mémoire simulant l'état de cache
   exact du cas signalé). L'architecte ou le PO doit rejouer manuellement le scénario (créer une
   déclaration fraîche sur `FC2501717`, charger l'écran ② Affectations une seule fois) après avoir
   **redémarré l'instance `Declaration.API`** — voir point 2.
2. **L'instance `Declaration.API` (PID 8312) tournant pendant cette vérification exécute
   toujours l'ancien binaire** (antérieur au correctif TASK-081, cf. warnings MSB3026 ci-dessus).
   Elle doit être arrêtée puis relancée après un rebuild propre pour que le correctif soit
   effectivement actif sur l'environnement où le PO a constaté le problème. Ce redémarrage n'a pas
   été effectué depuis cette session (hors périmètre : je n'ai pas arrêté de processus utilisateur
   sans consigne explicite).
3. Deux échecs de tests préexistants et environnementaux (accès SQL Server / données Sage
   manquantes) subsistent dans `Declaration.Selection.Tests` et `Declaration.Controle.Tests` —
   non liés à TASK-081, non introduits par cette vérification, mais signalés par honnêteté.
