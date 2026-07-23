# TASK-156 — Contention du rafraîchissement de valorisation OM : verrou anti-chevauchement + cache jamais servi pour les factures non payées

## Contexte
Signalement client (23/07/2026) : log serveur montrant `[VALO] batch OM en exception, repli individuel :
Timeout lors de l'exécution du worker en mode batch.` à deux reprises en quelques minutes. Diagnostic
architecte sur le log complet (13:58:24 → 14:09:28, `soId=1`) : **aucune perte de données** — le repli
individuel (TASK-023/072) a fonctionné comme prévu, chaque facture a fini par être traitée. Le vrai problème
est une **contention de ressource**, avec deux causes racines confirmées en lisant le code :

**A. Aucun verrou/anti-rebond partagé entre TOUS les appelants de l'orchestrateur OM pour un même `soId`**.
Constaté d'abord sur `RafraichirValorisationAsync`
([DeclarationWorkflowService.cs:558](../Declaration.Application/Services/DeclarationWorkflowService.cs#L558),
bouton **« Rafraîchir » de l'écran Factures) : le log montre **4 cycles qui se chevauchent** sur la même
société/période (13:58:24, 14:01:19, 14:01:30, 14:02:26) : le premier batch (973 pièces, lancé 13:58:24)
n'est revenu en exception qu'à 14:03:28 — **exactement 300 s**, le timeout batch codé en dur
([Declaration.Orchestration/WorkerInvoker.cs:101](../Declaration.Orchestration/WorkerInvoker.cs#L101)).
**Confirmé ensuite sur un chemin totalement différent** : le 23/07/2026 à 14:20:41, un nouveau batch de 361
pièces a été observé sans être précédé de la moindre ligne `[VALO-050]` (signature exclusive de
`RafraichirValorisationAsync`) — le PO confirme que ce traitement a été **lancé depuis une déclaration**
(action « Passer au calcul »/« Détail des lignes »). Vérification en code : `ChargerCandidatesSiNecessaireAsync`
([DeclarationWorkflowService.cs:144](../Declaration.Application/Services/DeclarationWorkflowService.cs#L144))
→ `ConstruireLignesFigeesAsync` (lignes 197-242) appelle le **même** `orchestrateur.Traiter()` sur le **même**
`soId`, sans aucun verrou partagé avec `RafraichirValorisationAsync`. **Le périmètre initial de ce correctif
(verrou posé uniquement autour du bouton Factures) n'aurait donc pas empêché l'incident du 23/07/2026 14:20
— il vient d'un autre appelant.** Recensement complet des appelants concernés (tous doivent partager le
même verrou par `soId`) :
1. `RafraichirValorisationAsync` (ligne 558/614) — bouton « Rafraîchir » écran Factures.
2. `ConstruireLignesFigeesAsync` (ligne 197-242, via `ChargerCandidatesSiNecessaireAsync` ligne 144) —
   ouverture/calcul d'une déclaration (« Passer au calcul »/« Détail des lignes »). **Chemin confirmé
   responsable de l'incident du 23/07/2026 14:20.**
3. Variante de réintégration de lignes libérées (lignes ~495-497).
4. `ResynchroniserLigneAsync` (ligne 521-543) — resynchronisation d'une seule pièce après correction Sage.

Quel que soit le déclencheur, tous concourent pour la même ressource (session OM/Sage, worker
`SageTaxReader.Console`) dès qu'ils portent sur le même `soId` — le tout se battant pour la même ressource.

**B. Le cache ne sert jamais une facture sans paiement pointé, contrairement à son intention documentée**
`TryServireDepuisCache`
([Declaration.Orchestration/OrchestrateurDeclaration.cs:407-420](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L407-L420))
retourne `null` dès que `currentToken == null` (« Facture dépayée »), donc toute facture `NonRapproche`/
`NonAffecte` est **relue via OM à CHAQUE cycle**, indéfiniment. Preuve dans le log : le même jeu d'une
trentaine de pièces `FF260xxx` (avoirs sans paiement pointé) est relu et réécrit en cache de façon identique
à 14:01:56, 14:03:33 et 14:07:33. Cela contredit le commentaire du code lui-même
([OrchestrateurDeclaration.cs:163-166](../Declaration.Orchestration/OrchestrateurDeclaration.cs#L163-L166)) :
*« On met en cache TOUTES les factures lues, même sans paiement pointé : la lecture OM ... est indépendante
du règlement — la relire plus tard serait du travail perdu »*. C'est cette relecture systématique qui
maintient le batch « pleine période » à ~973/1064 pièces à chaque rafraîchissement au lieu de diminuer avec
le remplissage du cache, et qui rend chaque cycle coûteux — donc plus susceptible d'entrer en contention
avec un cycle concurrent (cause A).

## Objectif
Éliminer la contention constatée sans changer le comportement fonctionnel de la valorisation (aucune ligne
de déclaration écrite par ce chemin, effet de bord = cache uniquement) :
1. Empêcher deux rafraîchissements concurrents de tourner en même temps pour la même société.
2. Faire que le cache soit réellement utilisé pour éviter de relire l'OM à chaque cycle pour les factures
   sans paiement pointé (dont le contenu OM — taux/base TVA — ne change pas tant que le paiement n'évolue pas).

## Périmètre STRICT
- **Correctif A** : `Declaration.Application/Services/DeclarationWorkflowService.cs` — **un seul verrou en
  mémoire par `soId`** (même pattern que `_figeageLocks`/`SemaphoreSlim` déjà utilisé ligne 35/148 pour le
  figeage), **partagé par les 4 appelants recensés ci-dessus** (`RafraichirValorisationAsync`,
  `ConstruireLignesFigeesAsync`, la variante de réintégration, `ResynchroniserLigneAsync`) — pas un verrou
  local à une seule méthode. Si un traitement OM est déjà en cours pour ce `soId` (peu importe lequel des 4
  chemins l'a démarré), **rejet immédiat** du second (décision PO, voir étape 1) plutôt que de relancer un
  second batch OM concurrent.
  ✅ **Confirmé par le PO (23/07/2026)** : rejet immédiat **uniforme sur les 4 chemins**, aucun traitement
  différencié selon l'appelant (pas d'attente silencieuse sur le chemin déclaration, pas de comportement
  spécial pour `ConstruireLignesFigeesAsync`) — même message d'erreur clair quel que soit le chemin qui a
  déclenché le rejet.
- **Correctif B** : `Declaration.Orchestration/OrchestrateurDeclaration.cs`, méthode `TryServireDepuisCache`
  (lignes 407-420) — servir le cache existant (montants OM déjà lus) même quand `currentToken == null`, en
  gardant intacte la règle métier « non déclarable sans paiement pointé » : c'est le **consommateur** du
  document (`resoudreFacture`/écran) qui doit continuer à exclure ces lignes de toute déclaration, pas la
  lecture du cache qui doit forcer une relecture OM inutile.
- **Aucune** modification de `SageTaxReaderService.cs` (worker OM), du schéma `DM_VENTILATION_SAGE_CACHE`,
  ni de la logique de déclaration/figeage.
- **Aucune** modification front (`declaration-tva-web`) — le bouton « Rafraîchir » continue de fonctionner à
  l'identique côté utilisateur ; seul le comportement serveur en cas de clics rapprochés change.

## Étapes
1. **Décision de comportement du verrou (A)** — ✅ **tranchée par le PO (23/07/2026) : option (b) — rejet
   immédiat.** Un second appel concurrent pour le même `soId` est **rejeté immédiatement** avec un message
   clair côté front (« Rafraîchissement déjà en cours »), plutôt que d'attendre silencieusement la fin du
   premier. L'utilisateur doit relancer manuellement une fois le premier cycle terminé.
2. Implémenter le verrou retenu (A) — `ConcurrentDictionary<int, SemaphoreSlim>` par `soId`, même pattern que
   `_figeageLocks`, **posé une seule fois (méthode privée commune) et appelé par les 4 sites recensés** :
   `RafraichirValorisationAsync`, `ConstruireLignesFigeesAsync`, la variante de réintégration,
   `ResynchroniserLigneAsync`. Vérifier qu'il ne bloque pas deux `soId` différents entre eux (multi-société
   doit rester parallèle), et qu'un appel sur le chemin déclaration (2/3/4) rejette bien un appel concurrent
   sur le chemin Factures (1) pour le même `soId`, et réciproquement (test croisé, pas seulement même-méthode).
3. Corriger `TryServireDepuisCache` (B) : retirer le court-circuit sur `currentToken == null` pour la lecture
   des montants OM (HT/TVA/TTC/parafiscal) — ne garder le contrôle du token que pour la décision « ligne
   déclarable ou non », déjà portée ailleurs dans le pipeline (voir commentaire ligne 167-168 de
   `OrchestrateurDeclaration.cs`, `TryGetToken` séparé de la lecture des montants).
4. Vérifier qu'un changement d'état de paiement (token qui apparaît/change) déclenche toujours une relecture
   quand nécessaire — ne pas casser la logique de fraîcheur existante pour les factures qui **deviennent**
   payées entre deux cycles.
5. Test de non-régression : rejouer `Declaration.Orchestration.Tests` (137 tests) + tests spécifiques au
   cache de ventilation — ajouter un test couvrant explicitement le cas « facture non payée, deux appels
   `Traiter` successifs → un seul appel OM » (actuellement absent, cause du bug B passée inaperçue).
6. Test de non-régression du verrou (A) : deux appels concurrents sur le même `soId`, **y compris entre
   deux chemins différents** (ex. `RafraichirValorisationAsync` + `ConstruireLignesFigeesAsync` en même
   temps) → un seul batch OM lancé, le second rejeté avec message clair ; deux `soId` différents → parallèle
   conservé, quels que soient les chemins combinés.
7. Build solution complète (`dotnet build DeclarationTVA.slnx`) : 0 erreur.
8. Si possible, rejouer en conditions réelles (base client ou copie) : déclencher deux rafraîchissements
   rapprochés et confirmer qu'un seul batch OM est lancé, et que le nombre de pièces à lire diminue d'un
   cycle à l'autre pour les factures déjà en cache.

## Livrables
- Diff `DeclarationWorkflowService.cs` (verrou par `soId`) + `OrchestrateurDeclaration.cs` (cache servi
  indépendamment du token).
- Tests unitaires nouveaux/modifiés couvrant les deux correctifs.
- `VERIFY/TASK-156_verify.md` : logs avant/après (build, tests), et si possible logs réels montrant la
  diminution du nombre de pièces à lire d'un cycle à l'autre pour une même société.

## Critères de validation
- Deux traitements OM concurrents sur le **même** `soId` ne lancent plus deux batchs OM en parallèle,
  **quels que soient les 2 chemins combinés parmi les 4 recensés** (pas seulement deux appels de la même
  méthode).
- Deux traitements sur des `soId` **différents** restent indépendants (pas de blocage croisé).
- Une facture sans paiement pointé, déjà lue une fois, n'est **plus** relue via OM au cycle suivant (sauf
  changement réel de son état de paiement) — vérifié par test unitaire dédié.
- Aucune ligne auparavant "non déclarable" ne devient déclarable par erreur (la règle métier du token reste
  intacte, seule la source de la lecture des montants change).
- `dotnet build DeclarationTVA.slnx` 0 erreur, `Declaration.Orchestration.Tests` 100 % vert (137 + nouveaux).

## Risques / dépendances
- **Décision UX à valider par le PO avant codage** (étape 1) : attente silencieuse vs rejet visible en cas de
  clics rapprochés sur « Rafraîchir ». Impact perçu différent selon le choix.
- **Confirmé après coup (23/07/2026 14:20)** : un incident réel a été déclenché depuis une déclaration
  (`ConstruireLignesFigeesAsync`), pas depuis le bouton Factures — preuve que le verrou doit couvrir les 4
  appelants recensés, pas seulement `RafraichirValorisationAsync`. Un verrou posé uniquement sur ce dernier
  serait **insuffisant** et laisserait le bug se reproduire dès qu'un autre chemin est sollicité en même
  temps.
- **Message de rejet sur le chemin déclaration** : ✅ résolu — PO confirme le rejet immédiat uniforme sur les
  4 chemins, y compris `ConstruireLignesFigeesAsync` (« Passer au calcul »). Aucun traitement différencié.
- **Risque de régression sur la fraîcheur du cache** si le retrait du court-circuit `currentToken == null`
  est mal borné : bien vérifier que la revalidation d'incohérence Sage (TASK-072/076, lignes 430-459 du même
  fichier) continue de s'appliquer même en l'absence de token — ne pas la contourner par erreur.
- **Non bloquant, indépendant** des autres chantiers ouverts (TASK-155, DDP TASK-127-136) — ne touche que le
  chemin de rafraîchissement d'affichage, jamais la déclaration/figeage elle-même.
- Le contournement immédiat (redémarrage du service côté client) reste valable en attendant cette task —
  cf. échange du 23/07/2026, aucune urgence à traiter avant confirmation PO du comportement souhaité (étape 1).
