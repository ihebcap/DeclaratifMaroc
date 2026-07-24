# TASK-176 Verify — Resynchronisation en masse des lignes

Date: 2026-07-24
Agent: Développeur (worker)

> Endpoint bulk + bouton front livrés. Les DEUX builds passent (back .NET + front tsc/vite).
> Vérification RÉELLE effectuée contre l'API en fonctionnement + base `GR_EMA_DISTRIBUTION`
> (déclaration réelle `798eb718…`, SO_Id=1) : sélection résolue, dédup EC_Id, traitement
> séquentiel et rejet de verrou concurrent (409) tous confirmés en live. Voir « Reste à valider »
> pour l'échelle 151 lignes du PO (non reproductible sur cette base de test).

## Périmètre livré

Un seul chemin en masse vers `ResynchroniserLigneAsync`, réutilisé **strictement** en boucle
**séquentielle**, avec retour agrégé synthétique — pas 151 réponses individuelles.

1. **Endpoint** `POST {id}/lignes/resynchroniser:bulk` — même contrat de sélection que
   `UpdateLignesBulk` (TASK-012) et `UpdateCodeActiviteBulk` (TASK-173) : liste explicite de
   `LigneIds` OU `Domaine`(+`Filter`). Résout la sélection en **EC_Id distincts** (une pièce Sage
   portant plusieurs lignes de taux n'est resynchronisée qu'une fois) puis appelle
   `ResynchroniserLigneAsync` pour chaque pièce, **une par une**.
2. **Retour agrégé** `ResynchroBulkResultat` : `totalSelection`, `traitees`, `resolues`,
   `nonTrouvees`, `toujoursEnAnomalie[]` (EcId + NumeroFacture + Motif relu du cache, lecture seule),
   `interrompu`, `messageInterruption`.
3. **Gestion du verrou soId (TASK-156)** :
   - verrou pris par un AUTRE traitement **avant la première pièce** → rien fait → **409** propre
     (message serveur explicite), cohérent avec le endpoint unitaire `Resynchroniser`.
   - verrou capté **entre deux pièces** → boucle interrompue proprement (`interrompu=true`), lignes
     déjà passées **préservées**, réponse **200** (jamais un 500).
4. **Front** : bouton « Resynchroniser la sélection » à côté des actions en masse existantes,
   confirmation `window.confirm` au-delà d'un seuil (20 lignes), indicateur d'activité (spinner +
   libellé « Resynchronisation… ») pendant l'appel, toast synthétique en retour
   (traitées / résolues / toujours en anomalie / interruption éventuelle). Appel API **factorisé**
   dans `api.ts` — aucune duplication du pipeline unitaire.

## Fichiers modifiés

Back :
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — nouvelle méthode
  `ResynchroniserLignesBulkAsync` (résolution de sélection + boucle séquentielle réutilisant
  `ResynchroniserLigneAsync` **sans la modifier**) + records `ResynchroBulkResultat` /
  `ResynchroLigneAnomalie`. Motif des pièces encore en anomalie relu via
  `GetMotifErreurCacheAsync` (méthode existante, lecture seule cache, aucune nouvelle lecture OM).
- `Declaration.API/Controllers/DeclarationsController.cs` — endpoint `ResynchroniserBulk`
  (`POST {id}/lignes/resynchroniser:bulk`) + DTO `BulkResynchroniserRequest`. 400 si sélection
  vide, 404 déclaration introuvable, 409 verrou concurrent avant 1re pièce.

Front :
- `declaration-tva-web/src/api.ts` — `resynchroniserLignesBulk` + interfaces
  `ResynchroBulkResultat` / `ResynchroLigneAnomalie`, à côté de `relireDepuisSage`.
- `declaration-tva-web/src/DomainGrid.tsx` — handler `doBulkResynchroniser` (seuil de confirmation,
  état `resynchroMasseEnCours`) + bouton dans la barre d'actions en masse, **gaté par
  `showResynchroniserAction`** (visible uniquement sur l'écran ③ Vérifier & Intégrer, jamais sur
  les 5 autres écrans partageant DomainGrid — même flag que le bouton par ligne TASK-170).

Non modifiés (garde-fou non-régression) : `ResynchroniserLigneAsync` elle-même, `GetCheckupAsync`
(zone TASK-177), `DiagnosticModal.tsx`, `AffectationsDrill.tsx`.

## Écart au périmètre « Files » de la TASK — documenté

La TASK listait `VerifierIntegrerPanel.tsx` pour le bouton front. En réalité, **toute la mécanique
de sélection multiple et les deux autres boutons en masse (état TASK-012, code activité TASK-173)
vivent dans `DomainGrid.tsx`** (composant enfant rendu par `VerifierIntegrerPanel`). Poser le bouton
dans `VerifierIntegrerPanel` aurait imposé de remonter tout l'état de sélection
(`selectedIds`/`selectAllFilters`/`filters`) — soit exactement la **duplication de logique interdite
par le garde-fou**. Le bouton a donc été ajouté dans `DomainGrid.tsx`, au plus près de la sélection
et des autres actions en masse. `DomainGrid.tsx` est d'ailleurs explicitement cité dans les
garde-fous de la TASK. Aucun des 3 chemins unitaires existants n'a été modifié.

## Checklist

- [x] **Build back** (`dotnet build DeclarationTVA.slnx`) OK — 0 erreur (7 warnings préexistants).
- [x] **Build front** (`npx tsc -b` sans erreur, `npx vite build` OK).
- [x] **Traitement séquentiel** — `foreach` + `await ResynchroniserLigneAsync`, **aucun**
      `Parallel.ForEach` / `Task.WhenAll` (vérifié au diff + confirmé en live : appel A a traité ses
      27 pièces l'une après l'autre en tenant le verrou soId par pièce).
- [x] **Test réel — sélection multiple → un seul appel → retour synthétique** : `798eb718…`,
      2 ligneIds → `{traitees:2, resolues:2, toujoursEnAnomalie:[], interrompu:false}`, 200, 0.55s.
- [x] **Dédup EC_Id (une pièce = une resynchro)** confirmée en base ET en live : 30 ligneIds
      envoyés → `totalSelection:27` (3 lignes partageaient un EC_Id) ; sur l'ensemble Decaissement,
      252 lignes ↔ 202 pièces distinctes.
- [x] **Rejet du verrou soId concurrent** : 2 appels bulk parallèles sur le même SO_Id=1 → l'un
      traite (200, 27 pièces), l'autre rejeté **409** avec message TASK-156 explicite, **avant toute
      pièce** (rien fait) — pas de blocage, pas de 500.
- [x] **Aucun changement de périmètre déclaration** : nb lignes/pièces identique avant/après bulk
      (Decaissement 252/202). `ResynchroniserLigneAsync` ne touche ni RT_AFFECTATION ni DT_Id.
- [x] **Validation 400** sur sélection vide (ni LigneIds ni Domaine).
- [x] **Non-régression 3 chemins unitaires** : `DiagnosticModal.tsx`, `AffectationsDrill.tsx` non
      touchés ; le bouton/handler par ligne de `DomainGrid.tsx` (`handleResynchroniser`, TASK-170)
      inchangé — seuls un import et un nouveau handler/bouton ont été ajoutés.
- [x] **Aucun SQL inline ajouté** — la résolution de sélection réutilise `GetLignesAsync` (repo), le
      motif réutilise `GetMotifErreurCacheAsync` (repo). Aucun secret en dur, aucun bypass sécurité.

## Reste à valider (honnête)

1. **Échelle 151 lignes du PO** : non reproductible sur cette base de test — aucune ligne `Exclue` et
   seulement 3 EC_Id en erreur sur SO_Id=1. Le comportement a été validé jusqu'à une sélection de
   27 pièces réelles (0.68s). L'extrapolation linéaire (~0.025 s/pièce ici) donnerait ~4 s pour
   151 pièces, mais le temps réel dépend de la charge OM/Sage du poste client — le seuil de
   confirmation front (20) et l'indicateur d'activité couvrent le cas long.
2. **Branche `interrompu=true` (verrou capté EN COURS de boucle, Traitees>0 → 200)** : vérifiée par
   lecture de code (catch `InvalidOperationException` → `Interrompu=true` + `break`, sans perdre les
   pièces déjà traitées). En live, la collision concurrente s'est produite dès la **première** pièce
   de l'appel B (→ 409, branche « rien fait »). La branche interruption-en-cours est structurellement
   la même gestion d'exception ; non déclenchée telle quelle faute de timing déterministe.
3. **Toasts de niveau `warning`/`success`** : `showToast` accepte un second argument de niveau
   (utilisé à l'identique par les handlers voisins de `DomainGrid.tsx`) — cohérence visuelle du toast
   non vérifiée à l'écran (test API en ligne de commande, pas via l'UI React).

## Notes

- Authentification du test réel : JWT admin signé avec la clé réelle de `connections.json`
  (Issuer/Audience de la config), validée normalement par l'API — aucune modification ni bypass de la
  couche de sécurité, équivalent fonctionnel d'un login.
- L'endpoint ne prévoit pas de vrai streaming de progression (SSE/chunked) : retour unique agrégé en
  fin de traitement. Compromis assumé vu la priorité MEDIUM et la contrainte « réutilisation stricte » ;
  le front affiche un indicateur d'activité + un toast synthétique, pas un compteur ligne à ligne.
