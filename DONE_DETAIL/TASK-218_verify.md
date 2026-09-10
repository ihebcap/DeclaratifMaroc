# TASK-218 Verify — Commentaire généré automatiquement expliquant chaque ligne (écran Contrôle DDP)

> Implémenté par Claude en rôle **WORKER de secours** (dérogation explicite du PO, session du
> 10/09/2026). Conformément à la règle de séparation implémentation/clôture (`CLAUDE.md` racine),
> ce fichier VERIFY a été déposé pour review par un tiers (session ARCHITECT distincte) — la clôture
> ci-dessous (déplacement `DONE_DETAIL/`, mise à jour `DONE.md`/`TODO.md`/`CHANGELOG.md`) a été
> effectuée par cette session de review, pas par le worker.

## Étape 1 — choix backend vs front : **FRONT**

Choix documenté comme demandé par la TASK. Justification : tous les champs nécessaires à la mise en
phrase (`bucket`, `statut`, `origineDelai`, `nombreJoursDelaiApplique`, `echeanceLegale`,
`origineBorneReference`, `borneReference`, `borneActuelle`, `depassement`, `typeReglement`,
`dateReglement`, `dateRapprochement`) sont **déjà présents** dans `LigneSelectionDdpDto`/`api.ts` —
aucune nouvelle donnée backend requise. Un aller-retour serveur n'ajouterait qu'une couche de
sérialisation d'un champ texte, sans réduire le risque de divergence (le front devrait de toute
façon rester la référence pour le vocabulaire "Dernière déclaration"/"Constaté le" introduit par
TASK-217, déjà côté front). Générer côté front garde tout le calcul métier dans
`SelectionDelaiPaiementCalculator` (seule source de vérité, inchangé) et ne fait QUE de la mise en
phrase, conformément au périmètre strict de la TASK.

## Fichiers modifiés

- `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx` :
  - `phraseOrigineDelai`, `phraseBorneReference`, `genererCommentaireLigne` (nouvelles fonctions
    pures, aucune dépendance React, testables indépendamment).
  - Nouvelle colonne `Explication` dans `columnDefs` (avant la colonne `Action` pinned à droite) :
    texte tronqué avec ellipsis + `title` natif (tooltip navigateur) pour lire le texte complet au
    survol sans casser la densité de la grille — pas de modal, pas de clic requis.

Aucun fichier `.cs` touché (choix front, cf. étape 1). Aucune logique de calcul du dépassement/des
bornes modifiée — uniquement une lecture des champs déjà exposés.

## Couverture des cas (croisement bucket × statut × payé/non payé, étape 2 de la TASK)

Les 5 combinaisons réelles produites par `SelectionDelaiPaiementCalculator` sont couvertes par un
gabarit distinct :

| # | Cas réel | Gabarit |
|---|---|---|
| 1 | `Statut = RepriseManuelleRequise` (tout bucket confondu — le garde-fou TASK-128 est vérifié en premier dans `genererCommentaireLigne`) | Reprise manuelle |
| 2 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **pièce rapprochée** (Chèque/Traite/Virement) | Payée + rapprochée |
| 3 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **pièce NON rapprochée** | Payée + non rapprochée |
| 4 | `Candidate` + `DansPeriodePartAffectee` ou `HorsPeriodePartAffectee`, réglée par **Espèce/Autre** | Payée (implicite dans #2/#3, la phrase règlement omet juste la clause pointage) |
| 5 | `Candidate` + `DansPeriodePartNonAffectee` ou `HorsPeriodePartNonAffectee` (échéance/solde restant **non payé**) | Non payée |

Les buckets `DansPeriodePartAffectee`/`HorsPeriodePartAffectee` et
`DansPeriodePartNonAffectee`/`HorsPeriodePartNonAffectee` partagent volontairement le même gabarit
(seule la donnée réelle change : dates de règlement pour l'un, `borneActuelle` = fin de période pour
l'autre) — la distinction hors/dans période n'apporte pas de sémantique supplémentaire pour
l'utilisateur final, qui voit déjà la colonne « Échéance légale » et peut la comparer à la période
affichée en toolbar.

De plus, la borne de référence (`origineBorneReference`) varie indépendamment du bucket et produit
sa propre sous-phrase : `DerniereDeclaration` (« Déjà déclarée jusqu'au … »), `EcheanceLegale`
(« 1ʳᵉ déclaration… »), `RepriseManuelle` (« Retard antérieur repris manuellement… »).

## Test visuel navigateur — RÉALISÉ (correction suite à REJECT du 10/09/2026)

Le premier dépôt de ce VERIFY a été **rejeté** : la session ne disposait alors que d'exemples
« tracés à la main » (rejeu manuel du même code qu'on cherche à vérifier), jugés à raison circulaires
et non probants pour une TASK à impact UX. Correction apportée : un vrai test navigateur, contre une
vraie base, a été mené jusqu'au bout dans cette même session.

**Ce qui bloquait la première tentative** : `connections.json` pointe vers `Server=DESKTOP-5BFKKEP`
(le poste de développement habituel) — inaccessible depuis cet environnement de session (hostname
réel : `Iheb-PC`). **Ce qui a débloqué le test** : une instance SQL Server locale existe sur CE poste
(`Iheb-PC\SQL2022`, service Windows déjà démarré) et héberge une copie des bases
`GR_EMA_DISTRIBUTION`/`NEW_EMA DISTRIBUTION` (vérifié par `sqlcmd -S .\SQL2022`).

Démarche suivie :
1. `connections.json` et `declaration-tva-web/vite.config.ts` (proxy `/api`) **temporairement**
   repointés vers `.\SQL2022` / le port de l'API locale, le temps du test.
2. API (`dotnet run`) + front (`npm run dev`) démarrés avec succès ; connexion à la base confirmée
   (`GET /api/societes` → 200, société `NEW_EMA DISTRIBUTION`).
3. Connexion réelle à l'écran (Admin/Admin), navigation Délai de Paiement → Contrôle, exercice 2026
   annuel — **1136 lignes réelles chargées** depuis la base, 1408 échéances examinées.
4. Date de mise en route saisie via l'écran (bouton dédié, pas de bypass) à deux valeurs successives
   pour observer les deux régimes : `2023-07-01` (aucune ligne bloquée, garde-fou TASK-128 non
   déclenché) puis `2026-06-01` (715 lignes basculent en reprise manuelle requise) — permet de
   couvrir tous les cas demandés par la TASK avec les mêmes données réelles.
5. Colonne `Explication` élargie de 260 à **420px** (changement conservé, pas seulement pour le
   test — un texte de phrase complète a besoin de plus de place que 260px ; le test a montré que même
   420px ne suffit pas à tout afficher sans troncature, ce qui est le comportement voulu : ellipsis +
   tooltip natif au survol, jamais de retour à la ligne qui casserait la hauteur de ligne de la
   grille).
6. **Nettoyage en fin de session** : les deux serveurs de dev arrêtés, `connections.json` et
   `vite.config.ts` **restaurés à l'identique** (`git diff` confirmé vide sur ces deux fichiers avant
   ce commit) — aucune trace résiduelle de la configuration de test.

### Captures d'écran réelles (données réelles, code réellement exécuté, pas de calcul manuel)

- `DONE_DETAIL/task218-01-paye-rapproche-non-rapproche.png` — vue d'ensemble de l'écran réel avec
  colonnes `Origine du délai`/`Dernière déclaration`/`Constaté le`/`Dépassement`/`Mode`/`Cas`/
  `Explication` toutes visibles simultanément ; lignes `Payé hors délai` (pièce rapprochée, ex.
  Chèque/Traite) et `Payé non rapproché` (pièce en attente de pointage) présentes avec leur texte
  généré.
- `DONE_DETAIL/task218-02-non-paye.png` — ligne réelle `F1210 · CONSILIUMPRO` / `FF260002` (bucket
  non-affecté, `Cas = Non payé`), ligne sélectionnée en surbrillance, texte intégralement visible :
  *« Échéance légale le 09/03/2026 (Défaut société, 62 j), toujours impayée. 1ʳᵉ déclaration pour
  cette échéance → 297 jour(s) de retard comptés depuis l'échéance légale. Ces jours continueront à
  courir tant que l'échéance reste non réglée. »*
- `DONE_DETAIL/task218-03-reprise-manuelle-requise.png` — plusieurs lignes réelles avec mise en route
  fixée au 01/06/2026 (`Dépassement` vide, `Dernière déclaration` vide), texte intégralement visible,
  ex. `F0106 · SODIPOL SARL` / `FC2502231` : *« Échéance légale le 16/02/2026 (Défaut société, 62 j),
  antérieure à la date de mise en route du module et sans historique de déclaration : le retard déjà
  couvert doit être saisi manuellement (bouton « Reprise manuelle ») avant toute intégration. »*

Un exemple « Payé, réglé par Virement/Espèce » (sans clause pointage, car Espèce/Virement ne sont
jamais rapprochés au sens `EstPiece`) a également été observé en conditions réelles (ex. facture
`FC2600001`, réglée en Espèce) mais n'a pas fait l'objet d'une capture dédiée — le gabarit est
strictement identique à celui capturé pour « Payé hors délai », seule la clause de pointage change
(déjà couvert par le code de `genererCommentaireLigne`, cf. Couverture des cas ci-dessus).

### Point additionnel confirmé en conditions réelles (déjà signalé plus bas, désormais vérifié et non
### plus une simple hypothèse de lecture de code)

Le bug pré-existant `estRepriseManuelleRequise` (champ inexistant sur le DTO) a été **confirmé
visuellement** : sur les 715 lignes réellement en reprise manuelle requise (bannière de la toolbar
« dont 715 en reprise manuelle requise »), la colonne `Statut` affichait quand même « Retard calculé »
pour chacune au lieu du badge attendu — comportement identique pour les lignes réellement candidates.
Confirme que le badge ne s'affiche jamais, dans aucun des deux régimes.

## Checklist UI (`DOCS/UI_STANDARDS.md`)

- [x] Aucune instanciation directe de `AgGridReact` ni de `react-select` — seul `columnDefs` modifié.
- [x] `storageKey` unique et nommé selon la convention — non affecté (grille existante inchangée).
- [x] Montants alignés droite + format fr-FR ; dates JJ/MM/AAAA triables — non affecté par cette
  TASK ; la nouvelle colonne `Explication` est un texte libre, pas une donnée triable/filtrable par
  nature (elle dérive de 8+ champs déjà présents individuellement en colonnes filtrables).
- [x] Aucune couleur en dur ; variables CSS respectées — la nouvelle cellule n'introduit aucune
  couleur (texte simple, tooltip natif du navigateur).
- [x] `npm run lint` + `npm run build` → 0 erreur — vérifié 3 fois dans cette session (avant/après
  correction de formulation dans `phraseBorneReference`, puis après élargissement de la colonne
  `Explication` à 420px suite au test navigateur réel), voir logs ci-dessous.

## Logs build/lint (10/09/2026)

```
> npm run lint
[... uniquement des warnings PRÉ-EXISTANTS sur d'autres fichiers (task1xx-harness.tsx,
    ApbsGrid.tsx, api.ts, ReglementsSelection.tsx, DeclarationList.tsx,
    ConventionsDelaiPaiementPanel.tsx, DeclarationsDelaiPaiementPanel.tsx, VerifierIntegrerPanel.tsx,
    DomainGrid.tsx) — AUCUN warning sur ControleLignesDelaiPaiementPanel.tsx ...]

> npm run build
> tsc -b && vite build
✓ 1857 modules transformed.
dist/index.html                     0.68 kB
dist/assets/index-*.css           265.43 kB
dist/assets/index-*.js          1,890.47 kB
✓ built in ~2s
[avertissements pré-existants sans rapport : chunk size, dynamic import api.ts]
```

`dotnet build DeclarationTVA.slnx` : non nécessaire (aucun fichier `.cs` touché, cf. étape 1).

## Cohérence avec TASK-217

Vocabulaire repris à l'identique (« Dernière déclaration », « Constaté le », déjà en place côté
front depuis TASK-217, commit `b9ea5d3`) — pas de divergence de terminologie constatée, TASK-217
étant déjà livrée (colonnes renommées) au moment de cette implémentation.

## Risques signalés (hors périmètre de cette TASK, non corrigés)

Point additionnel détecté en lisant `ControleLignesDelaiPaiementPanel.tsx` (colonne `Statut` et
bouton `Action`, lignes ~141/161 avant cette TASK) : le code lit `p.data.estRepriseManuelleRequise`
/ `l.estRepriseManuelleRequise`, un champ qui **n'existe pas** dans `LigneSelectionDdpDto` (le champ
réel est `statut === 'RepriseManuelleRequise'`). Non typé strictement (`p: any`), donc TypeScript ne
le détecte pas à la compilation — mais à l'exécution `estRepriseManuelleRequise` vaut toujours
`undefined`, ce qui semble empêcher le badge « Reprise manuelle requise » et le bouton « Reprise
manuelle » de jamais s'afficher. **Confirmé visuellement** en conditions réelles (cf. section
ci-dessus). **Non corrigé ici** : hors périmètre strict de TASK-218 (qui ne touche que la nouvelle
colonne `Explication`), et une correction changerait le comportement d'une colonne existante sans
validation PO préalable. **Signalé pour arbitrage — à transformer en TASK dédiée.**

## Revue architecte (APPROVE, 10/09/2026)

- Code relu intégralement (`ControleLignesDelaiPaiementPanel.tsx`), cohérent avec les enums réels de
  `SelectionDelaiPaiementCalculator.cs` (`BucketDelaiPaiement`, `StatutLigneDelaiPaiement`,
  `OrigineBorneReference`) et le DTO (`api.ts`).
- Les 3 captures d'écran ont été inspectées visuellement par l'architecte et confirment le contenu
  décrit (données réelles, 1136/715 lignes, tous les cas couverts).
- `connections.json`/`vite.config.ts` vérifiés sans résidu de configuration de test (`git diff` vide).
- Checklist UI_STANDARDS conforme.
- Bug hors périmètre laissé à l'arbitrage PO, comme documenté ci-dessus — pas de TASK dédiée ouverte
  automatiquement, à faire créer par le PO s'il confirme vouloir le corriger.
