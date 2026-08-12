# TASK-202 — Refonte navigation TVA : 4 écrans à responsabilité unique (remplace TASK-178)

Status: 🆕 à faire
Priority: MEDIUM — dette de clarté sur un écran d'usage quotidien, pas un bug bloquant
Module: declaration-tva-web

> **Origine :** signalement PO (06/08/2026, capture d'écran) — « je suis pas convaincu des écrans de
> la TVA, y'a des redondances, des écrans cachés ». Confirme et durcit le ressenti déjà exprimé le
> 24/07/2026 (TASK-178). Cadrage arbitré en session (06/08/2026, deux rounds), **passé de 3 à 4
> écrans** au 2ᵉ round parce que le PO a lui-même jugé la 1ʳᵉ version de l'écran ③ « Vérifier &
> Intégrer » toujours trop complexe (« très compliqué… ajouter une étape plutôt que rendre ça plus
> compliqué ») :
> 1. Intégration des règlements = écran ① Sélection existant, **inchangé**.
> 2. Affichage des factures à déclarer = **nouvel écran persistant**, actions en ligne conservées.
> 3. **Vérifier** = contrôles pré-intégration, anomalies, diagnostic — **purement consultatif**, pas
>    de bouton de confirmation.
> 4. **Confirmer** = récap final (chiffres dédupliqués) + bouton « Confirmer intégration », activable
>    seulement si l'étape ③ est propre.

## Constat (preuve de code, cartographie architecte du 06/08/2026)

L'écran ② actuel (`VerifierIntegrerPanel.tsx`, libellé « Vérifier & Intégrer », fusion historique
TASK-090 de deux anciens écrans ③+④) mélange trois responsabilités : récap chiffré, contrôles/
anomalies, **et** accès aux lignes de factures — ce dernier accès n'existant que via deux boutons
(« Codes activité », « Toutes les lignes / Resynchroniser ») qui **remplacent tout l'écran** par une
instance de `DomainGrid` (état interne `drillFiltre`, `VerifierIntegrerPanel.tsx:656-757`), sans être
un écran du stepper à part entière. `DomainGrid` elle-même (la grille où le comptable travaille,
TASK-170) n'est **jamais visible par défaut** dans ce flux — exactement le « écran caché derrière le
bouton » signalé par le PO.

Doublons de chiffres confirmés dans le code (certains explicitement documentés comme volontaires,
d'autres non) :
- Nombre de règlements sélectionnés : `RecapCard` (filtré par onglet actif) vs total de l'écran ① —
  le code reconnaît lui-même le risque de lecture contradictoire (`VerifierIntegrerPanel.tsx:1191-1194`).
- Total TVA : `RecapCard` **et** ligne « Σ Total » du tableau des sous-totaux par taux — doublon
  commenté comme volontaire (lignes 927-931), à réévaluer.
- Nombre d'anomalies bloquantes : calculé et rendu **3 fois** (bandeau haut, `ChecklistCard`, pied de
  grille près du bouton « Confirmer intégration »).
- Écart de cohérence : texte dans `ChecklistCard` **et** tableau détaillé juste en dessous
  (`RecapSourceTable`) — deux représentations du même écart.

## Décisions PO (arbitrage 06/08/2026)

1. **TASK-178 est remplacée par cette TASK** — son hypothèse (écran récap = point d'entrée unique
   pour l'action sur une ligne) est l'inverse de la demande actuelle (séparer l'affichage des lignes
   du récap). Le sous-problème réel qu'elle documentait (4 chemins redondants vers l'action
   « resynchroniser » : Diagnostiquer→`DiagnosticModal`, bouton ligne `DomainGrid` TASK-170,
   ex-écran ② Affectations, futur bulk TASK-176) est repris ci-dessous, dans le nouveau cadrage.
2. **Le futur écran 2 (Factures à déclarer) garde les actions en ligne** : resynchroniser (ligne et
   en masse), éditer le code activité (ligne et en masse), saisir la TVA d'un solde initial — tout ce
   que fait déjà `DomainGrid` avec `showResynchroniserAction` et `codeActiviteOptions` aujourd'hui,
   mais **fusionné en une seule vue** au lieu des deux variantes de drill actuelles
   (`kind: 'codeActivite'` vs `kind: 'toutes'`), qui n'ont aucune raison métier d'être séparées.
3. **L'ex-écran récap unique est scindé en deux (arbitrage 06/08/2026, 2ᵉ round)** — le PO a jugé,
   maquette à l'appui, que regrouper contrôles + anomalies + récap chiffré + bouton de confirmation
   sur un seul écran restait « très compliqué » même une fois les doublons retirés. Décision : ajouter
   une étape plutôt que densifier l'écran existant.
   - **③ Vérifier** : contenu de contrôle/diagnostic — `ChecklistCard` (3 items : Affectations
     valides, Cohérence des totaux déclarés, Absence d'anomalies bloquantes), `RecapSourceTable`
     (montants à l'origine de l'écart), tableau « lignes non valorisées » + bouton Diagnostiquer,
     liste des anomalies bloquantes + « Voir lignes », avertissements repliables, bandeau verdict en
     tête. **Aucun bouton de confirmation ici** — écran de lecture/investigation uniquement.
   - **④ Confirmer** : contenu de synthèse — les 4 cellules `RecapCard`, le tableau « Sous-totaux par
     taux TVA » (sans la ligne « Σ Total », doublon), export de contrôle (Excel), bouton « Confirmer
     intégration ». Le bouton reste désactivé si l'étape ③ signale une anomalie bloquante restante —
     avec un lien explicite « Voir le détail (étape ③) », pas une simple désactivation muette.
   - Exigence inchangée : **un seul jeu de chiffres par métrique**, réparti entre les deux écrans
     (pas de doublon À L'INTÉRIEUR d'un écran, et pas de doublon ENTRE ③ et ④ non plus — le nombre
     d'anomalies bloquantes, par exemple, n'existe que dans le bandeau verdict de ③, ④ se contente de
     dire « bloqué » sans recompter).
4. **Décision complémentaire (arbitrage 06/08/2026, 2e round) — statut de ligne devient binaire.**
   Précision du PO : **ce n'est pas un changement de règle métier** — c'est la reconnaissance que le
   contrôle existe déjà, un cran plus haut. Pour reporter ou écarter une facture, il suffit de **ne
   pas cocher le règlement correspondant sur l'écran ① Sélection** : une ligne n'est candidate en
   écran ② que si son règlement a été sélectionné en ①. Les actions de ligne « Exclure » et
   « Reporter » (décision indépendante par ligne, au sein d'un règlement déjà sélectionné) sont donc
   **redondantes** avec ce mécanisme existant, pas un nouveau comportement. **Une ligne est donc soit
   `Proposée` (candidate, son règlement est sélectionné), soit `Intégrée`** — plus de 3ᵉ/4ᵉ état.
   Décidé pour **tout le modèle**, pas seulement l'UI de l'écran ② (recherche, réconciliation, filtres
   inclus). **Aucune migration de données** : confirmé par le PO que ces statuts n'ont jamais été
   utilisés en production — pas de ligne `Exclue`/`Reportée` réelle à requalifier.
   > ⚠️ **Point de vigilance à signaler, pas à trancher seul** : un règlement peut affecter
   > plusieurs factures. Avant cette simplification, un comptable pouvait sélectionner un règlement
   > puis décider ligne par ligne d'exclure/reporter une seule de ses factures. Après, la seule
   > granularité de décision est le règlement entier (écran ①) — si ce cas d'usage (traiter
   > différemment deux factures d'un même règlement) existe en pratique, le signaler au PO avant de
   > clore le développement plutôt que de le découvrir après coup.

## Principe directeur de conception

**Simple dans la structure, complet dans l'information.** Chaque écran ne doit porter qu'une seule
responsabilité (cf. §Décisions), mais ça ne veut pas dire moins d'information affichée — c'est le
découpage qui doit être simple, pas le contenu. Aucune donnée aujourd'hui visible (colonnes,
compteurs, statuts, motifs de blocage) ne doit disparaître ou passer derrière un clic supplémentaire
par souci d'épure. Le seul contenu à retirer est la **répétition** du même chiffre à plusieurs
endroits (§Constat) — jamais une information qui n'existait qu'une fois.

## Maquette de référence (littérale, pas illustrative)

`TASKS/assets/TASK-202-maquette.html` (artifact partagé au PO le 06/08/2026) reprend **tel quel** les
libellés du code actuel — aucune colonne, aucun bouton, aucun statut n'y est inventé. Toute ambiguïté
d'implémentation se résout en relisant cette maquette avant de improviser un libellé ou une colonne
absente des deux fichiers ci-dessous.

## Périmètre STRICT

- **Inclus** :
  1. **Nouvel écran ② « Factures à déclarer »**, ajouté au stepper (`DeclarationStepper.tsx`) entre
     ① Sélection et l'écran récap (qui devient ③) — persistant, pas un état `drillFiltre` qui
     remplace un autre écran. Affiche `DomainGrid` avec les onglets Achats/Ventes existants
     (libellés réels : « TVA Déductible (Achats) » / « TVA Collective (Ventes) »).
  2. **Colonnes fusionnées**, réunion exacte de `codeActiviteColumns` et des colonnes par défaut +
     `showResynchroniserAction` (`VerifierIntegrerPanel.tsx:17-43`, `DomainGrid.tsx:154-164`) : N°
     Facture, Tiers, Origine, Montant HT, Taux TVA, Montant TTC, Source, **Code activité** (éditable,
     `key: codeActivite`), Statut, Motif Écartement, + colonne **Actions** (bouton « Resynchroniser »
     par ligne, ou « Saisir TVA » quand le motif commence par `Solde initial :`, cf.
     `DomainGrid.tsx:16, 666-680`). Toutes optionnelles via le `ColumnSelector` existant — ne retirer
     aucune colonne de la liste disponible, seul leur affichage simultané par défaut peut être ajusté
     si la densité gêne la lecture (cf. §Points à trancher).
  3. Actions en masse sur sélection, adaptées de `DomainGrid.tsx:468-503` : **« Intégrer » /
     « Réinitialiser » uniquement** — « Exclure » et « Reporter » sont **retirés** (cf. §Décisions,
     point 4 : redondants avec le fait de ne pas cocher le règlement en écran ①), ainsi que
     « Resynchroniser la sélection » et l'affectation en masse du code activité (select + bouton
     « Affecter »), conservés à l'identique.
  4. **Scission de l'ex-écran récap en ③ Vérifier et ④ Confirmer** (cf. §Décisions point 3) :
     - **③ Vérifier** reçoit `ChecklistCard` (3 items), `RecapSourceTable`, tableau « lignes non
       valorisées » (+ Diagnostiquer), liste des anomalies bloquantes (+ « Voir lignes »),
       avertissements repliables, bandeau verdict. Aucune des 4 cellules `RecapCard` ici — ce sont des
       chiffres de synthèse, pas des contrôles.
     - **④ Confirmer** reçoit les 4 cellules `RecapCard`, le tableau « Sous-totaux par taux TVA »
       (ligne « Σ Total », `VerifierIntegrerPanel.tsx:935`, **retirée** — doublon exact de la cellule
       « Total TVA à intégrer »), export de contrôle (Excel), bouton « Confirmer intégration ».
     - Le compteur d'anomalies bloquantes n'existe qu'au bandeau verdict de **③** ; l'item checklist
       « Absence d'anomalies bloquantes » ne répète pas le nombre (juste OK/KO), et **④** ne fait que
       relayer l'état global (« bloqué, voir étape ③ ») sans recalculer de compteur.
  5. Navigation « Voir lignes » (sur une anomalie/avertissement, dans ③) et clic sur une ligne d'écart
     (`RecapSourceTable`, dans ③) : au lieu de remplacer l'écran sur place (mécanisme `drillFiltre`
     actuel), naviguent vers l'**écran ② avec un filtre pré-appliqué** — un seul mécanisme
     d'affichage des lignes, plus deux (drill interne + écran séparé). Depuis ④, un blocage renvoie
     vers ③ (pas directement vers ②) pour garder le fil contrôle→correction→confirmation.
  6. Consolidation des chemins redondants vers l'action « resynchroniser » (repris de TASK-178) :
     avec l'écran ② comme point d'entrée naturel pour l'action de ligne, statuer sur le sort du
     bouton « Relire depuis Sage » interne à `DiagnosticModal` (le garder en lecture seule, l'action
     réelle passant par l'écran ②, ou le retirer) et sur l'éventuelle redondance côté ex-écran ①
     Affectations.
  7. Les boutons « Codes activité » et « Toutes les lignes / Resynchroniser » **disparaissent du pied
     de l'ex-écran ③** — leur contenu vit désormais en permanence sur l'écran ②. L'écran ④ ne porte
     que : Export de contrôle (Excel), Confirmer intégration.
- **Exclu** :
  - Aucun changement de règle métier de **calcul** (TVA, valorisation, motifs de blocage) — seule la
    simplification du statut de ligne (point 4 ci-dessus) est incluse, et ce n'est pas vécu comme une
    nouvelle règle mais comme le retrait d'une redondance avec l'écran ①.
  - `DiagnosticModal` reste pour le diagnostic détaillé d'**une** ligne (4 blocs d'explication) — ce
    n'est pas un « écran caché » problématique en soi, seul le sort de son bouton d'action interne
    est à trancher (point 5 ci-dessus).
  - Export de contrôle Excel, bouton Confirmer intégration : logique inchangée, seul leur emplacement
    dans le nouveau découpage à documenter.
  - Écran ① Sélection : non touché (cf. TASK-200, en cours par ailleurs).

## Points à trancher pendant le développement (non bloquants, à documenter dans le VERIFY)

- Fusionner les colonnes « Codes activité » et « Toutes les lignes / Resynchroniser » en une seule
  vue peut alourdir visuellement la grille (toutes les colonnes/actions en permanence) — si la
  lisibilité en pâtit, proposer un filtre/toggle de colonnes plutôt que deux écrans séparés (jamais
  revenir à un mécanisme de remplacement plein écran).
- Sort du bouton « Relire depuis Sage » dans `DiagnosticModal` (cf. point 5) — à documenter dans le
  VERIFY avec la décision prise, pas à trancher unilatéralement sans le signaler.

## Fichiers impactés par la simplification du statut (point 4) — recensement grep, à vérifier un par un

Front (`Exclue`/`Reportée` référencés) :
- [declaration-tva-web/src/DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) (boutons en masse à retirer).
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (texte de réconciliation « N exclue(s), N reportée(s) » à retirer des messages d'écart, cf. lignes ~1414/1432).
- [declaration-tva-web/src/AffectationsDrill.tsx](../declaration-tva-web/src/AffectationsDrill.tsx) (à vérifier — même statut probablement exposé).
- [declaration-tva-web/src/WorkstationPanel.tsx](../declaration-tva-web/src/WorkstationPanel.tsx) (à vérifier).
- [declaration-tva-web/src/mockServer.ts](../declaration-tva-web/src/mockServer.ts) (jeu de données de test à adapter si des lignes `Exclue`/`Reportée` y figurent).

Back (statut probablement modélisé/persisté côté serveur — à confirmer, portée réelle à évaluer par le
développeur avant de coder) :
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs)
- [Declaration.Application/Interfaces/IDeclarationRepository.cs](../Declaration.Application/Interfaces/IDeclarationRepository.cs)
- [Declaration.Application/Entities/WorkflowEntities.cs](../Declaration.Application/Entities/WorkflowEntities.cs)

> Ce recensement est un point de départ (grep littéral sur les mots `Exclue`/`Reportée`), pas une
> garantie d'exhaustivité — le développeur doit vérifier si un enum/`statutLigne` côté back porte
> ces valeurs en dur (contrat d'API avec le front) avant de considérer le retrait complet.

## Livrables

- Stepper à jour à **4 étapes** : ① Sélection, ② Factures à déclarer, ③ Vérifier, ④ Confirmer — plus
  de vue plein-écran qui remplace un autre écran.
- Écran ③ Vérifier : contrôles/anomalies/diagnostic, purement consultatif, aucun bouton de
  confirmation.
- Écran ④ Confirmer : récap chiffré dédupliqué + export + bouton Confirmer, désactivé avec renvoi
  explicite vers ③ si anomalie bloquante restante.
- Navigation « Voir lignes »/clic-écart (dans ③) redirigeant vers l'écran ② avec filtre pré-appliqué.
- Statut de ligne binaire (`Proposée`/`Intégrée`) partout où `Exclue`/`Reportée` existaient — boutons,
  textes de réconciliation, contrat back si applicable (cf. recensement ci-dessus).
- `VERIFY/TASK-202_verify.md` : preuve sur cas réel — parcours complet ①→②→③→④ sur une déclaration
  avec anomalies bloquantes, vérification qu'aucune action existante n'a été perdue (resync, code
  activité, saisie TVA, diagnostic), capture des chiffres uniques (répartis entre ③ et ④, jamais
  dupliqués entre les deux), confirmation qu'aucune ligne `Exclue`/`Reportée` ne subsiste dans le
  code ou en base.

## Critères de validation

- Aucune action aujourd'hui possible (resynchroniser ligne/masse, éditer code activité ligne/masse,
  saisir TVA solde initial, diagnostiquer, exporter, confirmer) n'est perdue — seule leur
  présentation/emplacement change.
- Chaque métrique (règlements sélectionnés, total TVA, anomalies bloquantes, écart de cohérence)
  n'apparaît qu'**une fois**, à l'écran qui lui correspond (③ contrôle, ④ synthèse) — jamais sur les
  deux.
- Le nouvel écran ② est accessible directement depuis le stepper, sans passer par un bouton qui
  remplace un autre écran. L'écran ③ n'a pas de bouton de confirmation ; l'écran ④ n'a pas de liste
  d'anomalies détaillée (juste l'état global + renvoi vers ③).
- Non-régression complète sur le calcul et les statuts de ligne (aucune règle métier de calcul
  modifiée — seule la simplification décrite au point 4 des Décisions).
- Retest fonctionnel réel du parcours complet ①→②→③→④ par le PO avant clôture (écran à usage
  quotidien).

## Dépendance ajoutée (arbitrage PO 07/08/2026) — TASK-204 doit être développée avant celle-ci

Décision de migrer toutes les grilles maison vers **AG Grid Community**
(`TASKS/TASK-204-migration-ag-grid-community.md`), y compris `DomainGrid.tsx` que cette TASK-202
prévoyait d'étendre par fusion de colonnes (`codeActiviteColumns` + `showResynchroniserAction`,
§Périmètre point 2). **Ne pas développer TASK-202 avant TASK-204** : l'écran ② « Factures à
déclarer » doit être construit directement avec des `columnDefs` AG Grid (mêmes colonnes/actions
décidées ci-dessus, sur le nouveau moteur), pas par extension des props de l'ancien `DomainGrid.tsx`
— sinon double travail sur le même fichier avec deux moteurs de grille différents. Le contenu
fonctionnel (colonnes, actions, statut binaire) décidé dans cette TASK reste valable ; seule la
technique d'implémentation change.

## Risques / dépendances

- **Volume de retest important** : cette TASK touche la navigation d'un écran central du flux TVA,
  utilisé quotidiennement — prévoir un retest complet, pas seulement une vérification de compilation.
- Dépend indirectement de TASK-176 (futur bulk resync) et TASK-177 (déblocage validation
  incohérence) : si ces TASKS sont en cours, séquencer pour éviter des éditions concurrentes de
  `DomainGrid.tsx` / `VerifierIntegrerPanel.tsx`.
- Remplace TASK-178 : ne pas développer TASK-178, cf. bandeau ajouté en tête de ce fichier.

## Files

- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (écran récap actuel, mécanisme `drillFiltre` à retirer).
- [declaration-tva-web/src/DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) (grille à exposer en écran persistant, fusion des colonnes codeActivite + resync).
- [declaration-tva-web/src/DeclarationStepper.tsx](../declaration-tva-web/src/DeclarationStepper.tsx) (ajout de l'étape ② dans le stepper).
- [declaration-tva-web/src/DiagnosticModal.tsx](../declaration-tva-web/src/DiagnosticModal.tsx) (sort du bouton « Relire depuis Sage » interne, point à trancher).
