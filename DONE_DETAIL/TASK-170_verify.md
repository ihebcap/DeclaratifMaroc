# TASK-170 Verify — Bouton « Resynchroniser » direct dans l'écran ③ Vérifier & Intégrer

> Implémentation réalisée en tant que worker (exception ponctuelle accordée explicitement par le PO
> pour cette seule task, en dérogation au rôle architecte habituel de CLAUDE.md). Front seul, aucun
> fichier back touché. Build front (`tsc -b` + `vite build`) passe. **Rejeu réel effectué le
> 24/07/2026** contre l'environnement réel (`DESKTOP-5BFKKEP`, `GR_EMA_DISTRIBUTION`/`NEW_EMA
> DISTRIBUTION`, login `Admin` fourni par le PO) : voir « Test réel effectué » ci-dessous. Point
> restant : le scénario exact « montant corrigé sur Sage puis vérifié » n'a pas pu être rejoué
> (aucune facture pré-arrangée avec une correction Sage fraîche disponible dans cette session).

## Décision de conception retenue

**Confirmée explicitement par le PO (24/07/2026, en session)** : le bouton est visible sur **toutes
les lignes** du domaine sélectionné, pas seulement celles déjà en anomalie — c'est précisément le cas
rapporté par le PO (ligne d'apparence saine, donnée Sage source corrigée après coup) que les 3
chemins existants ne couvraient pas. Ce n'est plus seulement une recommandation architecte suivie par
défaut : le point ouvert §2 de la TASK est tranché.

## Test réel effectué (24/07/2026)

Rejeu réel en navigateur (Playwright, Chromium headless) contre l'environnement réel du poste
(`connections.json` → `DESKTOP-5BFKKEP`, `GrfConnection`/`PersistenceConnection` = `GR_EMA_DISTRIBUTION`,
Sage résolu dynamiquement par `SO_Id` → `NEW_EMA DISTRIBUTION`), API .NET (`dotnet run`, profil dev)
et front Vite lancés en local, authentifié avec le compte `Admin` (identifiants fournis par le PO en
session). Reproduction :

1. Connexion réussie, liste réelle des 6 déclarations (`TVA1-2026-01` à `06`, mêmes montants que ceux
   documentés dans `DOCS/AUDIT-TASK-143`) — confirme la connexion à la vraie base, pas une fixture.
2. Ouverture de `TVA1-2026-01` → onglet « ② Vérifier & Intégrer » → clic sur le nouveau bouton
   « Toutes les lignes / Resynchroniser » → grille ouverte, **242 lignes réelles**, colonne
   « Actions » présente avec un bouton « Resynchroniser » sur **chaque ligne visible (19/19 sur la
   page)**, y compris des lignes `Statut: Proposée` **sans aucun motif d'anomalie** (capture
   `13-before-click-resync.png`) — confirme visuellement l'objectif central de la task (bouton non
   conditionné à un statut d'anomalie).
3. Clic sur « Resynchroniser » d'une ligne saine (facture `FC2600035`) → requête réelle
   `POST /api/declarations/{id}/lignes/resynchroniser` → **200, `{"resolue":true}`** → toast vert
   « Ligne FC2600035 resynchronisée depuis Sage. » affiché immédiatement (capture
   `13b-just-after-click.png`) → grille rechargée sans erreur. Aucune exception, aucun 409 (verrou
   `soId` TASK-156 non déclenché ici, aucun autre traitement OM concurrent au moment du test), aucune
   erreur console/réseau.
4. Non-régression vérifiée : bascule sur l'onglet « 1. Sélection » → **0** bouton « Resynchroniser »
   présent (opt-in confirmé, pas de fuite de la colonne « Actions » vers un écran qui ne la demande
   pas).

**Portée du test réel, honnêtement délimitée** : ce rejeu prouve que le câblage bout-en-bout
fonctionne contre le vrai back/vraie base/vrai Sage (requête réelle, vraie session OM, réponse
`resolue:true` sans erreur) — il ne prouve PAS le scénario exact demandé par la checklist d'origine
(« montant corrigé sur Sage → clic → montant à jour en base/écran »), qui suppose une facture
préalablement modifiée côté Sage par le comptable/PO ; aucune telle facture n'était disponible/
identifiée dans cette session. Le point 3 de la TASK-170 (« vérifier en conditions réelles ») reste
donc à confirmer par le PO sur un cas réel de correction Sage.

Anomalie mineure sans rapport avec le code TASK-170, rencontrée pendant la mise en place du test :
`declaration-tva-web/.env.development` fixe `VITE_API_BASE=http://localhost:5018/api` alors que
`Declaration.API/Program.cs:102-103` lie toujours Kestrel sur `ServerConfig:Port` (5000 par défaut,
`connections.json` de ce poste) — les deux ports divergent en dev local. Contournement pris pour ce
test (variable d'env `VITE_API_BASE` positionnée au lancement, aucun fichier commité modifié) ;
signalé ici pour information, **non corrigé** (hors périmètre TASK-170, à ouvrir séparément si le PO
le juge utile).

## Périmètre livré

`VerifierIntegrerPanel.tsx` n'affiche jamais individuellement « toutes les lignes » d'un domaine en
dehors des vues de drill déjà existantes (`DomainGrid`) — la seule vue déjà capable de lister
l'intégralité des lignes, y compris saines, était le drill « Codes activité » (bouton existant,
`filtre: {}`). Plutôt que de surcharger ce bouton à vocation différente, un **nouveau bouton dédié**
a été ajouté à côté :

1. **Nouveau bouton « Toutes les lignes / Resynchroniser »** (`VerifierIntegrerPanel.tsx`, à côté du
   bouton « Codes activité ») : ouvre le même mécanisme de drill (`setDrillFiltre`, `kind: 'toutes'`),
   filtre vide, colonnes par défaut de `DomainGrid`.
2. **`DomainGrid.tsx`** : nouvelle prop opt-in `showResynchroniserAction?: boolean`. Quand elle est
   vraie, une colonne « Actions » supplémentaire (140px, hors du système `columns`/`colsStorageKey`,
   donc jamais masquable/persistée par erreur) affiche un bouton « Resynchroniser » par ligne
   (visible dès que `row.ecId > 0`, sans condition de statut). Au clic : appelle
   `relireDepuisSage(declarationId, row.ecId)` (déjà exporté par `api.ts`, TASK-167 — **aucun nouvel
   appel réseau créé**), puis :
   - si `resolue === true` : toast succès + `fetchPage()` (recharge la page courante) +
     `onActionDone()` (remonte au parent → `setReloadToken` → recharge `checkup`/RecapCard).
   - si `resolue === false` : toast avertissement explicite (« la ligne reste en anomalie, motif
     inchangé ») — **jamais de montant fabriqué**, conforme au garde-fou §3 de la task et au
     comportement déjà en place dans `DiagnosticModal.tsx`.
   - en cas d'exception (ex. 409 verrou `soId` partagé, TASK-156) : toast d'erreur avec le message
     serveur explicite, jamais une file d'attente silencieuse.
3. **Opt-in strict** : `showResynchroniserAction` n'est passé à `true` que depuis le nouveau drill
   `kind: 'toutes'` de `VerifierIntegrerPanel.tsx`. Les 5 autres écrans partageant `DomainGrid`
   (① Sélection, Workstation, ProofModal, DeclarationFinalePanel, drill incohérence, drill codes
   activité) ne passent pas cette prop → aucun changement visuel/fonctionnel pour eux.

## Fichiers modifiés

- `declaration-tva-web/src/DomainGrid.tsx` — colonne « Actions » opt-in + logique de resynchronisation
  (nouveau, ~70 lignes). **Non listé dans le § Files de la TASK d'origine** : ajout nécessaire car
  `VerifierIntegrerPanel.tsx` ne rend jamais lui-même les lignes individuelles, il délègue toujours à
  `DomainGrid` pour toute vue « liste de lignes ». Signalé explicitement ici pour que la review en
  tienne compte.
- `declaration-tva-web/src/VerifierIntegrerPanel.tsx` — nouveau bouton, nouveau `kind: 'toutes'` de
  drill, branchement de `showResynchroniserAction` sur ce seul drill, `onActionDone` du bloc drill
  relié à `setReloadToken` (auparavant `() => {}`, sans effet — nécessaire pour que le RecapCard/
  checkup se rafraîchisse après une resynchronisation réussie).

Aucun fichier back touché : `api.ts` (`relireDepuisSage`), `DiagnosticModal.tsx` et
`DeclarationWorkflowService.cs` (`ResynchroniserLigneAsync`) sont **réutilisés tels quels**, non
modifiés — conforme au garde-fou de la TASK (« pas de 5ᵉ appelant », « ne pas dupliquer la logique »).

## Checklist

- [x] Build front OK : `npx tsc -b` → 0 erreur ; `npm run build` (`tsc -b && vite build`) → 0 erreur
      (1 seul avertissement Vite préexistant, `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`/`Auth.tsx`,
      sans rapport avec cette task).
- [x] Bouton « Resynchroniser » visible et cliquable sur une ligne de l'écran ③, y compris une ligne
      sans anomalie apparente — **vérifié à l'écran** (Playwright, environnement réel, capture
      `13-before-click-resync.png` : 19/19 lignes de la page portent le bouton, y compris plusieurs
      `Statut: Proposée` sans motif).
- [x] Réutilisation stricte du chemin `ResynchroniserLigneAsync`/`relireDepuisSage`/verrou `soId`
      (TASK-156) — aucun nouvel endpoint, aucune nouvelle règle métier ajoutée ; **confirmé en
      conditions réelles** (requête réelle observée, réponse `200 {"resolue":true}`).
- [x] Aucun changement de `DT_Id`/périmètre : aucun code touchant ce champ n'a été modifié.
- [x] Non-régression des 2 chemins existants : `DiagnosticModal.tsx` et `AffectationsDrill.tsx` ne
      sont **pas modifiés** (diff nul sur ces deux fichiers).
- [x] Non-régression opt-in : **vérifié à l'écran**, 0 bouton « Resynchroniser » sur l'écran
      ① Sélection (capture `20-step1-selection.png`).
- [x] Décision PO tracée sur le périmètre (§2 de la TASK) — **confirmée explicitement par le PO en
      session (24/07/2026)** : toutes les lignes.
- [ ] Test réel du scénario exact de la checklist d'origine (montant corrigé sur Sage → clic →
      montant à jour en base et à l'écran) — **partiellement couvert** : le câblage bout-en-bout est
      vérifié en conditions réelles (requête réelle, vraie session OM, `resolue:true`, toast), mais
      aucune facture pré-corrigée sur Sage n'était disponible pour rejouer le scénario exact
      « avant/après correction ».

## Reste à valider (NON couvert par ce VERIFY — bloquant clôture définitive)

1. **Scénario exact de la checklist** : facture d'une déclaration figée, montant corrigé sur Sage,
   clic sur le nouveau bouton → vérifier le montant à jour dans `DM_VENTILATION_SAGE_CACHE` et à
   l'écran. Le câblage est vérifié réel (point ci-dessus) mais ce scénario précis (avec une
   correction Sage préalable) nécessite que le PO/comptable l'exécute sur un cas réel — aucune
   facture de ce type identifiée dans cette session.
2. Rappel non résolu, distinct de cette task (déjà noté dans TASK-170 §« Dépendances/risques ») :
   `ResynchroniserLigneAsync` peut annoncer `resolue:true` sans rien avoir réparé quand Sage renvoie 0
   ligne de taxe sans erreur — non traité ici, hors périmètre.
3. Anomalie mineure sans rapport avec cette task, signalée ci-dessus (§Test réel) : divergence de
   port entre `.env.development` (5018) et le port Kestrel réel piloté par `connections.json` (5000)
   en dev local — non corrigée, à ouvrir séparément si utile.

## Verdict

Implémentation conforme au périmètre technique de la TASK et aux garde-fous (réutilisation stricte,
aucune donnée fabriquée, opt-in sans régression — vérifié en code ET à l'écran). Décision PO sur le
périmètre (§2) obtenue. Câblage bout-en-bout vérifié en conditions réelles (requête réelle, vraie
session Sage OM, succès, non-régression écran ①). **Seul le point 1 ci-dessus (scénario exact
« correction Sage préalable » de la checklist d'origine) reste à faire exécuter par le PO/comptable
sur un cas réel** — ce n'est plus un doute sur le câblage, mais la confirmation d'un cas d'usage
précis qui n'appartient pas à ce qu'un worker peut arranger seul.
