# TASK-070 — Allégement du contenu de l'écran ③ Calcul TVA (retrait détail facture, redondant avec ②)

Status: TODO
Priority: MEDIUM
Risk: LOW
Module: Front — `declaration-tva-web` (tunnel déclaration TVA, écran ③)

## OBJECTIF
Retirer le tableau détail facture (`CalculTvaPanel.tsx:288-364`) de l'écran ③ Calcul TVA — ce détail existe déjà à l'écran ② Affectations (`AffectationsDrill.tsx`, colonne `Taux` filtrable + `grandTotalTva`). Ne conserver dans ③ que le bloc « Sous-totaux par taux TVA » (`CalculTvaPanel.tsx:366-402`) et le bandeau pied total (`CalculTvaPanel.tsx:441-474`, inchangé — option A de TASK-069 reconduite). Le bloc « lignes non valorisées — détail motif » (`CalculTvaPanel.tsx:404-438`) est conservé (transparence de traçabilité, hors redondance).

## BUSINESS VALUE
Le PO a identifié, en testant le correctif TASK-069, que l'écran ③ affiche un doublon du détail facture déjà présent en ② (constat : « je sais pas encore l'utilité de cet écran récapitulatif ... redondant »). Alléger ③ en un pur écran de validation finale (sous-totaux + total) avant l'action irréversible d'intégration :
- élimine la redondance UX signalée par le PO,
- résout de fait le bug de visibilité découvert lors du VERIFY de TASK-069 (le bloc « Sous-totaux par taux » était écrasé/peu visible car il partageait la zone de scroll avec le tableau détail facture — en le retirant, il n'y a plus rien pour l'écraser),
- sans les coûts de risque d'une fusion ②+③ (renumérotation du tunnel 6→5 étapes, ré-câblage `onCalcSummary`) — option écartée par le PO au profit de cet allègement.

## CONTRAINTES
- **Aucun changement de logique de valorisation ni d'agrégation** : `agregParFactureTaux`, `sousTotauxParTaux`, `onCalcSummary` (totalTVA/nbLignes remontés à ④ Intégration) restent strictement identiques — seul du JSX d'affichage est retiré.
- Le calcul de `rows` (via `agregParFactureTaux`) reste nécessaire en interne (il alimente `sousTotaux`, `nbNonValorise`, le bandeau pied et le bloc lignes non valorisées) — ne pas le supprimer, seulement ne plus le rendre en tableau détail.
- Aucun changement back / API / SQL.
- Aucun changement à `AffectationsDrill.tsx` (② reste la source du détail facture) ni à `DeclarationStepper.tsx` (le correctif layout TASK-069 est conservé tel quel — pas de nouvelle régression sur le conteneur partagé).
- Cohérence avec le principe « jamais un 0 muet » : le bloc lignes non valorisées (motifs) doit rester visible et complet après le retrait du tableau détail.

## FILES
- `declaration-tva-web/src/CalculTvaPanel.tsx` (retrait du bloc `288-364` « Tableau principal » ; vérifier que `sousTotaux.length > 0` reste la bonne condition d'affichage du bloc sous-totaux devenu le contenu principal de l'écran)

## VALIDATION
- [ ] Build OK (`npm run build` : tsc + vite, 0 erreur)
- [ ] Tests passés (aucun test automatisé connu sur ce composant — vérification manuelle/e2e)
- [ ] Écran ③ n'affiche plus le tableau détail facture ; seuls « Sous-totaux par taux TVA », le détail des lignes non valorisées (si présentes) et le bandeau pied total sont visibles
- [ ] Le bloc « Sous-totaux par taux TVA » est visible sans avoir à scroller sur un volume réaliste (reprendre le cas PO : 68 règlements / 245 lignes)
- [ ] `onCalcSummary(totalTVA, nbLignes)` remonte des valeurs identiques à avant (④ Intégration non affecté) — vérifier par comparaison des montants affichés en ④ avant/après
- [ ] Aucune régression sur les 5 autres étapes du stepper (aucun fichier partagé touché en dehors de `CalculTvaPanel.tsx`)
- [ ] Capture(s) d'écran réelles (navigateur) de l'écran ③ après correctif, avec le volume de données du cas PO

## ARCHITECTURE RULES APPLICABLES
- `ARCHITECTURE_PROJECT.md` / logique métier : aucune logique de valorisation dans l'UI — confirmé, ce correctif ne touche que du rendu.
- Règle projet : lecture seule stricte côté front sur cet écran (aucun état modifié) — inchangé.
- Décision PO actée (13/07/2026) : ne pas fusionner ②+③ — alléger ③ uniquement.

## NOTES
Origine : extrait du VERIFY de TASK-069 (voir `DONE_DETAIL/TASK-069-correctif-mise-en-page-ecran-calcul-tva.md`). Le PO avait initialement signalé un bandeau pied rogné (résolu par TASK-069, correctif stepper `minHeight:0`) ; en testant le correctif, il a révélé qu'un bloc distinct (« Sous-totaux par taux ») restait mal visible, et a spontanément questionné l'utilité du détail facture dupliqué avec ②. Deux options ont été proposées au PO : (A) retirer le détail facture de ③, garder les sous-totaux ; (B) fusionner ②+③ en ajoutant les totaux à ②. Le PO a retenu l'option A sur recommandation de l'architecte (risque plus faible, pas de renumérotation du tunnel, ② et ③ gardent des intentions distinctes : affecter vs. dernier contrôle avant intégration irréversible).
