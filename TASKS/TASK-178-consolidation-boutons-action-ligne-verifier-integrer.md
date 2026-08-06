# TASK-178 — Consolidation des boutons d'action sur une ligne (écran ③ Vérifier & Intégrer / écran ② Affectations)

Status: ❌ **REMPLACÉE PAR TASK-202** (arbitrage PO 06/08/2026) — ne pas développer.

> **Raison du remplacement :** cette TASK partait de l'hypothèse que l'écran ② Vérifier & Intégrer
> devait devenir le **point d'entrée unique** pour l'action sur une ligne (§Objectif point 1
> ci-dessous). Le PO a demandé le 06/08/2026 l'inverse : **séparer** l'affichage/action sur les lignes
> (« Codes activité » / « Toutes les lignes / Resynchroniser », aujourd'hui des vues plein-écran cachées
> derrière ces boutons) dans un **écran 2 distinct** du récap. Les deux architectures sont
> contradictoires — TASK-202 reprend le sous-problème réel documenté ici (4 chemins vers l'action
> resynchroniser, redondance Diagnostiquer/DomainGrid/AffectationsDrill) dans le nouveau cadrage à
> 3 écrans. Conservée ici pour trace, non développée.

Status (historique, avant remplacement) : 🆕 à faire — **DÉCISION DE CONCEPTION À TRANCHER PAR LE PO avant tout développement** (§Objectif)
Priority: LOW (confort/lisibilité, aucun bug — ne pas prioriser devant TASK-175/176/177)
Risk: MEDIUM à développer (petit volume de code, mais retest complet requis sur un écran d'usage
quotidien du PO) — pas de risque à NE PAS la faire, c'est une dette de clarté, pas une dette
fonctionnelle
Module: declaration-tva-web

> **Origine :** ressenti PO exprimé en session (24/07/2026), pendant la revue de TASK-175/176/177 :
> *« je sens beaucoup de complication dans l'écran, y'a beaucoup de boutons... je sais pas c'est
> vrai on a optimisé mais on peut faire mieux »*. Le PO n'est pas certain lui-même du découpage
> cible — cette TASK documente le constat et des pistes, elle ne prescrit pas une solution figée.

## Constat (preuve de code, vérifié pendant la session)

L'action « resynchroniser une pièce Sage » (`ResynchroniserLigneAsync`, TASK-078/167) est
aujourd'hui accessible par **4 chemins distincts**, chacun livré indépendamment pour un cas
signalé précis, jamais reconsolidés :

1. Bouton **« Diagnostiquer »** (écran ③, `VerifierIntegrerPanel.tsx:1360`, visible seulement sur
   les lignes du bloc « non valorisées ») → ouvre `DiagnosticModal.tsx` → bouton **« Relire depuis
   Sage »** dedans (conditionné à `!data.cachePerime`).
2. Bouton **« Resynchroniser »** direct par ligne (écran ③, `DomainGrid.tsx:265`, TASK-170 — livré
   justement pour éviter le détour par (1), mais sans retirer (1)).
3. Bouton **« Corriger / Resynchroniser »** (écran ② Affectations, `AffectationsDrill.tsx`,
   conditionné à `estIncoherente`).
4. Un futur bulk (TASK-176, en cours).

À côté, des actions **liées mais séparées** :
- **« Valider l'incohérence »** (`AffectationsDrill.tsx:443`) — n'existe QUE sur l'écran ②, alors
  que l'anomalie correspondante s'affiche aussi sur l'écran ③ (cf. TASK-177) : pour valider une
  ligne vue sur l'écran ③, le PO doit changer d'écran.
- 2 actions en masse déjà livrées (changement d'état, code activité TASK-173) + le futur bulk
  resync (TASK-176) : 3 mécanismes de sélection multiple à terme sur le même écran.
- « Recalculer depuis cache » (TASK-147), distinct de « Resynchroniser » (relit Sage) — distinction
  correcte fonctionnellement, mais un comptable doit déjà connaître la différence pour choisir le
  bon bouton.

Chaque bouton est individuellement justifié par sa TASK d'origine (traçable dans
`DONE_DETAIL/TASK-167`, `TASK-170`, `TASK-078`, `TASK-147`) — le problème n'est pas qu'un chemin
soit inutile, c'est l'absence de repasse de consolidation après plusieurs livraisons successives.

## Objectif

**Décision de conception à trancher par le PO avant tout développement** — cette TASK ne doit pas
être codée tant que les points ci-dessous n'ont pas de réponse :

1. L'écran ③ doit-il devenir le point d'entrée **unique** pour l'action sur une ligne (cohérent
   avec la recommandation déjà actée sur TASK-170 : « c'est l'écran où le comptable travaille
   réellement ») ? Si oui :
   - Ajouter « Valider l'incohérence » comme action directe sur l'écran ③ (à côté de
     « Resynchroniser »), pour ne plus obliger un aller-retour vers l'écran ②.
   - Regrouper les 2-3 actions par ligne (Resynchroniser / Valider incohérence / voir diagnostic
     complet) sous une seule affordance par ligne (menu contextuel, popover, ou petit groupe de
     boutons compacts) plutôt que des boutons dispersés selon l'état de la ligne.
   - Décider du sort du chemin (3) (`AffectationsDrill.tsx`, écran ②) : le retirer (redondant une
     fois (1)+(2)+« Valider » consolidés sur ③), ou le garder en secours pour un usage propre à
     l'écran ② (affectations, pas anomalies) ?
2. Le bouton « Diagnostiquer » (1) doit-il garder son propre bouton « Relire depuis Sage » interne
   à la modale, ou celle-ci devient-elle **lecture seule** (diagnostic uniquement) une fois que le
   bouton de ligne (2) couvre déjà l'action de resynchronisation ?
3. Les 3 mécanismes de sélection multiple à terme (état / code activité / resync) doivent-ils
   partager un seul menu « Actions sur la sélection » au lieu de 3 boutons distincts en pied de
   grille ?

**Une fois ces arbitrages posés par le PO**, le développement lui-même est mécanique : réutilisation
stricte des appels API déjà existants (`relireDepuisSage`, `valider-incoherence` déjà dans
`api.ts`), aucune nouvelle règle métier ni nouvel endpoint (hors ceux déjà couverts par TASK-176).

## Garde-fous

- **Ne pas commencer le développement avant l'arbitrage PO du §Objectif** — même pattern que
  TASK-152 : documenter les options, ne pas trancher seul.
- Aucune nouvelle règle métier : réutilisation stricte des endpoints/appels déjà livrés
  (`ResynchroniserLigneAsync`, `ValiderIncoherenceLigneAsync`, `RecalculerLigneDepuisCacheAsync`).
- Ne pas fusionner des actions qui ont des périmètres réellement différents (Resynchroniser relit
  Sage, Recalculer depuis cache ne relit rien) — la consolidation vise l'**accessibilité**, pas la
  simplification des règles métier elles-mêmes.
- Prioriser après TASK-175/176/177 (bugs réels) — cette TASK est de la dette de clarté, pas une
  dette fonctionnelle, ne doit jamais passer devant un correctif de bug en cours.

## Files

- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (écran ③, actions par ligne + bloc « non valorisées »).
- [declaration-tva-web/src/DomainGrid.tsx](../declaration-tva-web/src/DomainGrid.tsx) (bouton « Resynchroniser » TASK-170, ligne ~265).
- [declaration-tva-web/src/DiagnosticModal.tsx](../declaration-tva-web/src/DiagnosticModal.tsx) (bouton « Relire depuis Sage » interne).
- [declaration-tva-web/src/AffectationsDrill.tsx](../declaration-tva-web/src/AffectationsDrill.tsx) (écran ②, « Corriger / Resynchroniser » + « Valider l'incohérence »).
- [declaration-tva-web/src/api.ts](../declaration-tva-web/src/api.ts) (appels déjà existants, réutilisation seule).

## Validation

- [ ] Arbitrage PO obtenu et documenté sur les 3 points du §Objectif avant tout commit de code.
- [ ] Build front OK.
- [ ] Aucune action perdue : chaque action aujourd'hui accessible (resynchroniser, valider
      incohérence, diagnostiquer, recalculer depuis cache) reste possible après consolidation,
      seule leur présentation change.
- [ ] Non-régression sur les 3 chemins de resynchronisation existants et sur « Valider
      l'incohérence » (test réel, pas seulement compilation).
- [ ] Retest complet de l'écran ③ en conditions réelles (usage quotidien du PO) avant livraison.

## Dépendances / risques

- **Bloquée par construction** tant que l'arbitrage PO (§Objectif) n'est pas fait — ne pas
  planifier de créneau de développement avant ça.
- Dépend indirectement de TASK-177 (une fois « Valider l'incohérence » exposée sur l'écran ③, elle
  doit effectivement débloquer le contrôle bloquant — sinon la consolidation UI exposerait un
  bouton qui ne résout rien, reproduisant la confusion actuelle sous une autre forme).
- Risque principal : temps de retest sur un écran à fort usage quotidien, pas la complexité du code
  lui-même (cf. échange de session : « lourd en design/retest, pas en lignes de code »).
