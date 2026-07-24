# VERIFY — TASK-165 : simplification de l'écran ③ Vérifier & Intégrer

## Contexte
Signalement PO : écran ③ perçu comme trop chargé, informations redondantes (Total TVA, Règlements
sélectionnés, Lignes valorisées affichés 2 fois chacun). Arbitrage déjà tranché dans les
instructions de cette session (non redemandé) : **Option A — repli progressif**, déclinée en 6
changements listés dans la task, appliqués tels quels dans `VerifierIntegrerPanel.tsx`. Fait après
TASK-167 (même fichier connexe, `DiagnosticModal.tsx`) comme demandé, pour éviter un double travail.

## Ce qui est livré (les 6 changements de la recommandation architecte)

1. **Verdict remonté en tête d'écran** — nouveau bandeau en tête de la zone scrollable (avant les
   sous-totaux), réutilisant EXACTEMENT la même logique que le pied de grille existant
   (`hasBloquant`/`bloquants.length`, aucune nouvelle règle métier) : « ✅ Prêt à intégrer » / « ❌
   Bloqué — N anomalie(s), voir ci-dessous » / spinner pendant le chargement du checkup. Masqué en
   lecture seule (`isReadOnly`, un autre bandeau « Intégrée » existe déjà pour ce cas).
2. **`RecapCard` inchangé** — aucune ligne de son code touchée, reste l'unique porteur des chiffres
   de synthèse (règlements/lignes/total TVA).
3. **`ChecklistCard`/`buildControls` allégés** — les items `reglements` et `lignes` retirés de
   `buildControls` (back-front, aucune info au-delà de `RecapCard`) ; signature simplifiée en
   conséquence (`nbLignes`/`nbReglements`/`integree` retirés, devenus inutiles — respecte
   `noUnusedParameters` du projet). Ne restent que `affectations`, `equilibre`, `bloquants`.
4. **« Sous-totaux par taux TVA » replié par défaut** — nouveau `useState(sousTotauxOuvert)`,
   bouton d'en-tête affichant le nombre de taux (« Sous-totaux par taux TVA (5 taux) ») et un
   chevron ▼/▲ ; le tableau détaillé (et sa ligne « Σ Total », doublon assumé et documenté en
   commentaire — volontairement conservé car ce bloc est replié par défaut) ne s'affiche que si
   déplié.
5. **« Avertissements (non bloquants) » replié par défaut** — même patron (`useState` local à
   `ChecklistCard`), en-tête affichant uniquement le compteur (« N avertissement(s) (non
   bloquant(s)) ») + chevron, liste détaillée uniquement si dépliée.
6. **Fusion visuelle « Lignes non valorisées » ↔ item `equilibre`** — le tableau détaillé (facture/
   tiers/statut/motif/bouton Diagnostiquer), auparavant un bloc séparé de l'écran, est désormais
   rendu À L'INTÉRIEUR de `ChecklistCard` (juste après la liste des contrôles, avant le détail des
   « Anomalies bloquantes ») — un seul bloc « Anomalies » regroupe désormais contrôles + détail
   equilibre + lignes non valorisées + bloquants + avertissements. Nouvelles props
   `lignesNonValorisees`/`onDiagnostiquer` sur `ChecklistCard`, câblage du bouton « Diagnostiquer »
   identique à avant (`setDiagnostic({ ecId, factureNumero })`, aucun changement de comportement).

## Ce qui n'a PAS changé (garde-fous respectés)
- Aucune modification de calcul, d'endpoint ou de source de données (`/checkup`,
  `/declarations/{id}/lignes`) — uniquement du réagencement d'affichage front.
- Tous les callbacks existants restent câblés à l'identique : `onDrill`, `onDrillIncoherence`,
  `setDiagnostic`, le bouton « Codes activité » (TASK-161, hors sujet, non touché).
- Le tableau « Sous-totaux par taux TVA » (détail par taux) n'a pas été supprimé, seulement replié
  — aucune donnée retirée, uniquement son niveau de visibilité par défaut.
- S'applique mécaniquement aux DEUX onglets (Décaissement/Encaissement) — `VerifierIntegrerPanel`
  reste un composant unique piloté par `selectedTab`, aucune duplication de code par onglet.

## Vérification indépendante des critères de validation

- [x] **Chaque chiffre (règlements, lignes, total TVA) n'apparaît plus qu'une seule fois en
      premier niveau de lecture** — `RecapCard` reste l'unique porteur visible par défaut ; le
      total TVA du tableau « Sous-totaux » n'est visible qu'après dépliage explicite (second
      niveau, pas le premier) ; les items `reglements`/`lignes` de la check-list ont été retirés.
- [x] **Aucune perte d'information** — le tableau « Sous-totaux par taux TVA » et les
      « Avertissements » restent intégralement accessibles (repliés, pas supprimés) ; le tableau
      « Lignes non valorisées » est toujours rendu intégralement (même colonnes, même bouton
      Diagnostiquer), simplement déplacé à l'intérieur du bloc `ChecklistCard`.
- [x] **Non-régression des drills (TASK-016/107/112/139/142/144/161)** — `onDrill`,
      `onDrillIncoherence`, `setDiagnostic`, `ligneIncoherenteAutreOnglet` (TASK-139, croisement
      entre onglets) tous transmis à `ChecklistCard` sans modification de signature ni de valeur ;
      seul le rendu visuel de `ChecklistCard` a changé (ajout de deux sections, pas de retrait de
      logique existante). Confirmé par relecture du diff : aucune prop supprimée sur les callbacks
      existants, uniquement des props ajoutées (`lignesNonValorisees`, `onDiagnostiquer`).
- [x] **Build front 0 erreur** — `npm run build` (`tsc -b && vite build`) : succès, bundle généré
      (avertissement `INEFFECTIVE_DYNAMIC_IMPORT` préexistant, sans rapport). Un premier passage
      avec `npx tsc --noEmit` seul n'avait pas détecté une erreur de type réelle
      (`lignesNonValorisees` typé `LigneValorisation[]` au lieu du type réellement agrégé
      `RowAggr[]` utilisé par `filteredRows`) — seule `npm run build` (mode projet composite,
      `tsc -b`) l'a révélée ; corrigée immédiatement (type ajusté à `RowAggr[]`), build revérifié
      vert après correction. Leçon retenue pour la suite : `npm run build` fait foi, pas
      `tsc --noEmit` seul sur ce projet.
- [x] Preuve indirecte de déploiement réel : les nouvelles chaînes d'interface (« Prêt à
      intégrer », « Déplier », « Replier ») retrouvées dans le bundle de production généré
      (`declaration-tva-web/dist/assets/*.js`), confirmant que le code modifié est bien celui qui
      serait servi par l'application.

## Réserves non bloquantes
- **Aucune vérification visuelle en navigateur réel** (aucun outil d'automatisation navigateur
  disponible dans cette session) — ni capture d'écran avant/après, ni test manuel des DEUX onglets
  demandé explicitement par la task (« Point d'attention VERIFY : capturer/valider explicitement
  les DEUX onglets »). La vérification s'est limitée à : relecture exhaustive du diff (props/
  callbacks non cassés), build TypeScript strict (`noUnusedLocals`/`noUnusedParameters`), et
  présence des nouvelles chaînes dans le bundle compilé. **Le PO devrait valider visuellement les
  deux onglets avant de considérer cette task définitivement close**, en particulier le
  comportement de `ligneIncoherenteAutreOnglet` (TASK-139) qui dépend du croisement entre onglets.
- La ligne « Σ Total » du tableau « Sous-totaux par taux TVA » reste un doublon volontaire du
  `RecapCard` (documenté en commentaire) — accepté car ce bloc n'est visible qu'après dépliage
  explicite (second niveau de lecture), conformément à l'intention de l'Option A.
