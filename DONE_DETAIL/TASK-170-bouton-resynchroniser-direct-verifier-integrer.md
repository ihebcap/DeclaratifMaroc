# TASK-170 — Bouton « Resynchroniser » invisible/non découvrable dans l'écran ③ Vérifier & Intégrer

## Contexte
Suite directe de TASK-167. Le PO signale (24/07/2026), après avoir corrigé un montant de facture sur
Sage, ne pas voir de moyen de relancer une relecture Sage pour cette ligne **depuis l'écran ③ Vérifier &
Intégrer** : *« impossible de penser qu'il faut cliquer sur détail ligne »*.

## Constat (vérifié en lisant le code, pas supposé)
TASK-167 a bien livré une action de relecture Sage réelle (`relireDepuisSage` → endpoint
`POST {id}/lignes/resynchroniser` → `ResynchroniserLigneAsync`), mais elle n'est atteignable que par un
chemin à deux clics et conditionnel :

1. Dans `VerifierIntegrerPanel.tsx`, le bouton **« Diagnostiquer »** n'apparaît que sur les lignes déjà
   listées dans le bloc *« lignes non valorisées »* (`onDiagnostiquer`, ligne ~1327) — jamais sur une
   ligne valorisée en apparence saine.
2. Cliquer « Diagnostiquer » ouvre `DiagnosticModal.tsx`, où le bouton **« Relire depuis Sage »**
   n'apparaît lui-même que si `!data.cachePerime` (ligne 200) — une seconde condition, invisible depuis
   la liste.
3. Le seul autre chemin existant vers `ResynchroniserLigneAsync`, le bouton **« Corriger / Resynchroniser »**
   d'`AffectationsDrill.tsx` (écran ② Affectations, ligne ~690), n'apparaît lui aussi que si `estIncoherente`
   (bandeau rouge d'incohérence Sage détectée après figeage) — un troisième filtre distinct, sur un
   troisième écran.

**Aucun des trois chemins n'est un bouton direct, visible sur la ligne, dans l'écran ③.** Un comptable qui
vient de corriger une facture sur Sage et qui veut simplement « refaire lire cette ligne » n'a aujourd'hui
aucune affordance dans l'écran où il travaille réellement (Vérifier & Intégrer) — il doit deviner qu'il
faut ouvrir un panneau de diagnostic réservé aux anomalies, ou changer d'écran.

## Objectif
1. Ajouter, dans `VerifierIntegrerPanel.tsx`, un bouton **« Resynchroniser »** directement visible sur
   chaque ligne (colonne actions), sans dépendre de son statut d'anomalie — appelant le même endpoint
   `POST {id}/lignes/resynchroniser` déjà livré par TASK-167 (aucune nouvelle règle métier, aucun nouveau
   verrou : réutilisation stricte du chemin `ResynchroniserLigneAsync`/`ExecuterAvecVerrouOMAsync` déjà
   protégé par TASK-156).
2. **Décision de conception à trancher par le PO** : le bouton doit-il être visible sur **toutes** les
   lignes (y compris déjà valorisées sans anomalie apparente — cas exact du PO : correction Sage sur une
   ligne jusque-là saine), ou seulement sur les lignes actuellement en anomalie (repli minimal, cohérent
   avec TASK-167 mais qui ne résout pas le cas rapporté ici) ? **Recommandation architecte** : visible sur
   toutes les lignes — c'est précisément l'absence de ce cas (ligne saine mais donnée Sage source
   corrigée après coup) qui motive cette task ; restreindre aux seules lignes en anomalie reproduirait le
   même trou.
3. Retour utilisateur clair après clic (succès/échec, motif si toujours en anomalie) directement dans la
   ligne ou un toast — sans obliger à rouvrir un modal pour voir le résultat.
4. Une fois livré, vérifier en conditions réelles qu'un montant corrigé côté Sage est bien répercuté après
   clic sur ce nouveau bouton, sans changement de `DT_Id` ni sortie de la ligne de son domaine.

## Garde-fous
- Aucune nouvelle règle de lecture Sage : réutilisation stricte de `ResynchroniserLigneAsync` (TASK-078/167),
  donc du verrou `soId` partagé (TASK-156) — pas de 5ᵉ appelant.
- Jamais de montant fabriqué : si la relecture échoue encore, le motif réel reste affiché, bloquant.
- Jamais de changement de `DT_Id`/périmètre de déclaration par cette action.
- Ne pas dupliquer la logique déjà présente dans `DiagnosticModal.tsx`/`AffectationsDrill.tsx` — factoriser
  l'appel API (déjà `relireDepuisSage` dans `api.ts`) plutôt que réécrire un second appel équivalent.

## Files
- [declaration-tva-web/src/VerifierIntegrerPanel.tsx](../declaration-tva-web/src/VerifierIntegrerPanel.tsx) (ajout du bouton par ligne).
- [declaration-tva-web/src/api.ts](../declaration-tva-web/src/api.ts) (`relireDepuisSage`, déjà existant — réutiliser).
- [declaration-tva-web/src/DiagnosticModal.tsx](../declaration-tva-web/src/DiagnosticModal.tsx) (référence du patron déjà livré par TASK-167, ne pas dupliquer la logique).
- [Declaration.Application/Services/DeclarationWorkflowService.cs](../Declaration.Application/Services/DeclarationWorkflowService.cs) (`ResynchroniserLigneAsync` — aucune modification attendue, réutilisation seule).

## Validation
- [ ] Build front OK (`tsc --noEmit`, `npm run build`).
- [ ] Bouton « Resynchroniser » visible et cliquable sur une ligne de l'écran ③, y compris une ligne sans
      anomalie apparente (cas réel du PO).
- [ ] Test réel : montant corrigé sur Sage pour une facture d'une déclaration déjà figée → clic sur le
      nouveau bouton → montant à jour dans la ligne, vérifié en base (`DM_VENTILATION_SAGE_CACHE`) et à
      l'écran.
- [ ] Aucun changement de `DT_Id`/périmètre observé après l'action.
- [ ] Aucune régression sur les 2 chemins existants (`DiagnosticModal.tsx` « Relire depuis Sage »,
      `AffectationsDrill.tsx` « Corriger / Resynchroniser »).
- [ ] Décision PO tracée sur le périmètre du bouton (toutes lignes vs anomalies seules, §2).

## Dépendances / risques
- Dépend de l'arbitrage PO sur le périmètre (§2) avant développement front définitif.
- Risque : si le bouton est proposé sur des centaines de lignes sans anomalie, un usage massif/répété
  pourrait solliciter Sage OM inutilement — le verrou `soId` (TASK-156) empêche la contention mais pas le
  volume d'appels ; envisager un simple message de confirmation avant appel si le PO le juge utile.
- Rappel non résolu, distinct de cette task : `ResynchroniserLigneAsync` peut annoncer `resolue:true` sans
  rien avoir réparé quand Sage renvoie 0 ligne de taxe sans erreur (trouvé sur `TVA1-2026-04`,
  `EC_Id=18199/18198`, cf. campagne de test du 23-24/07/2026) — non traité ici, task séparée à ouvrir sur
  décision du PO.
