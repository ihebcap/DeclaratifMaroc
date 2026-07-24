# TASK-165 — Simplification de l'écran ③ Vérifier & Intégrer (redondances d'affichage)

## Contexte
Signalement PO (24/07/2026, capture écran ③ Vérifier & Intégrer, onglet TVA Déductible) : écran perçu
comme trop complexe, informations redondantes.

**Constat code (cartographie architecte, `declaration-tva-web/src/VerifierIntegrerPanel.tsx`)** : l'écran
empile aujourd'hui 4 blocs indépendants, ajoutés incrémentalement task par task (TASK-090, TASK-018,
TASK-112, TASK-139, TASK-142, TASK-144, TASK-161), sans jamais de passe de consolidation. Plusieurs
chiffres identiques sont affichés à deux endroits distincts de l'écran :

1. **Total TVA affiché deux fois** — la ligne « Σ Total » du tableau *Sous-totaux par taux TVA*
   (`VerifierIntegrerPanel.tsx:775-780`) affiche déjà `localTotalHT`/`localTotalTVA`/`localTotalHT+localTotalTVA`.
   Le `RecapCard` juste en dessous (`VerifierIntegrerPanel.tsx:835-841`, cellule « Total TVA à intégrer »)
   réaffiche la même valeur (`displayTotalTVA`, capture : 142 110,11 MAD dans les deux blocs).
2. **« Règlements sélectionnés » affiché deux fois** — cellule du `RecapCard` (`nbReglements`,
   `RecapCard` l.1052-1056) **et** 1ᵉʳ item de la `ChecklistCard` (`buildControls`, id `reglements`,
   l.1143-1149, même chiffre en description : « 67 règlement(s) en entrée »).
3. **« Lignes valorisées/sélectionnées » affiché deux fois** — cellule du `RecapCard` (`nbLignes`,
   l.1057-1062) **et** 2ᵉ item de la `ChecklistCard` (id `lignes`, l.1151-1157, même chiffre : « 240
   ligne(s) valorisée(s) prêtes à intégrer »).
4. **Un seul et même écran mélange 3 niveaux de détail sans hiérarchie visuelle claire** : synthèse
   chiffrée (sous-totaux + récap), check-list de contrôle (5 items OK/ATTENTION/BLOQUANT), **et** détail
   ligne-par-ligne des anomalies (bloc rouge « Anomalies bloquantes » + bloc orange « Avertissements »)
   — ces deux derniers blocs sont la version détaillée des items 4 et 5 de la check-list juste au-dessus
   (`ChecklistCard`, `VerifierIntegrerPanel.tsx:1296-1417`), rendant la lecture séquentielle longue pour
   une seule action finale (bouton "Confirmer intégration").
5. **Aucun lien visuel explicite entre le tableau « Lignes non valorisées »** (bloc 2 de l'écran,
   l.787-832) **et le détail « Cohérence des flux déclarés » en écart** (item `equilibre` de la
   check-list, l.1248-1288) — l'utilisateur doit déduire lui-même que ce sont potentiellement les mêmes
   lignes qui expliquent l'écart, alors que le code sait déjà les relier (`ligneIncoherente`,
   `estNonValorise`).

**Portée du diagnostic** : ceci est une observation de structure d'affichage (empilement de blocs
ajoutés task par task), pas un défaut de calcul — aucune valeur affichée n'est fausse, `source unique =
back` (principe anti-régression documenté en tête de fichier) reste respecté partout. Le risque est
uniquement de lisibilité/charge cognitive pour le comptable qui valide l'intégration.

## Décision de périmètre
**Nécessite arbitrage PO avant tout développement** : la fusion de blocs redondants est un choix de
design fonctionnel (quel chiffre garder visible en premier plan, quel niveau de détail replier), pas
une correction de bug — l'architecte ne tranche pas seul ce choix. Deux options possibles à arbitrer :
- **Option A (repli progressif)** : garder le `RecapCard` comme unique bloc de synthèse chiffrée en tête
  d'écran ; transformer la `ChecklistCard` en items de contrôle *sans* rappeler les chiffres déjà dans le
  `RecapCard` (ne garder que le statut OK/ATTENTION/BLOQUANT + description courte) ; replier
  « Anomalies bloquantes »/« Avertissements » sous un disclosure (`<details>` ou équivalent) au lieu d'un
  affichage systématique.
- **Option B (fusion complète)** : fusionner `RecapCard` et les 2 premiers items de la `ChecklistCard`
  (« Règlements sélectionnés », « Lignes valorisées ») en un seul bloc, la check-list ne portant plus que
  les contrôles qui ne sont *pas* déjà des chiffres de synthèse (affectations, équilibre, anomalies
  bloquantes).

Le tableau « Sous-totaux par taux TVA » et son total ne sont **pas** proposés à la suppression : c'est la
seule vue par taux (20/14/10/7/0 %), information non portée ailleurs — seul son total agrégé fait doublon
avec le `RecapCard`, pas le détail par taux lui-même.

**Point de vue métier (simulation comptable, architecte, 24/07/2026)** — ce que l'utilisateur final
attend en priorité de cet écran, du plus urgent au plus secondaire :
1. Un verdict unique en tête : prêt à intégrer OUI/NON, pas une check-list de 5 items à parcourir pour
   le déduire.
2. Si NON : un renvoi direct vers la cause (factures en anomalie), sans avoir à recouper soi-même la
   check-list, le bloc « Anomalies bloquantes » et le tableau « Lignes non valorisées ».
3. Les chiffres qu'il va signer (total TVA, nb lignes, nb règlements) affichés **une seule fois**, en
   évidence — les revoir dans un second bloc n'inspire pas confiance, ça fait douter que ce soit
   vraiment la même valeur.
4. Le détail par taux TVA : utile pour recoupement personnel, pas à chaque intégration → candidat au
   repli par défaut.
5. Les avertissements non bloquants : n'empêchent pas de valider, occupent aujourd'hui autant de place
   visuelle que les anomalies réellement bloquantes → hiérarchie à corriger.

Cette lecture penche pour l'**Option A (repli progressif)** : un seul verdict + un seul jeu de chiffres de
synthèse en premier niveau, le reste (sous-totaux par taux, avertissements, détail des anomalies)
accessible en second niveau (replié). Reste un avis d'architecte à valider par le PO, pas une décision
tranchée.

## Recommandation ferme de l'architecte (24/07/2026)
**Option A retenue**, déclinée en 6 changements concrets :
1. **Verdict remonté en tête d'écran** (avant les sous-totaux) : « ✅ Prêt à intégrer » / « ❌ Bloqué —
   N anomalie(s), voir ci-dessous » — aujourd'hui ce verdict n'existe qu'en pied de grille
   (`VerifierIntegrerPanel.tsx:889-905`), à dupliquer/remonter en tête de la zone scrollable.
2. `RecapCard` **inchangé** — reste l'unique porteur des chiffres de synthèse (règlements, lignes, total
   TVA).
3. `ChecklistCard`/`buildControls` **allégés** : retirer les items `reglements` et `lignes`
   (l.1143-1157) — aucune information au-delà de ce que porte déjà `RecapCard`. Ne restent que
   `affectations`, `equilibre`, `bloquants`.
4. Tableau « Sous-totaux par taux TVA » (l.746-784) → **replié par défaut** (disclosure fermée à
   l'ouverture de l'écran, compteur de taux visible sur l'en-tête repliée).
5. Bloc « Avertissements (non bloquants) » (l.1358-1417) → **replié par défaut**, seul le compteur visible
   sur l'en-tête (« 7 avertissements »).
6. Fusionner visuellement le tableau « Lignes non valorisées » (l.787-832) et le détail de l'item
   `equilibre` en écart (l.1248-1288) en un seul bloc « Anomalies » — le code relie déjà les deux
   (`ligneIncoherente`, `estNonValorise`), seul l'affichage les sépare aujourd'hui.

**Ce qui ne change pas** : tous les callbacks existants (`onDrill`, `onDrillIncoherence`, `setDiagnostic`,
drill « Codes activité ») restent câblés à l'identique — seul l'emballage visuel est modifié.

**Confirmation PO (24/07/2026) : les deux onglets sont concernés.** Aucun développement distinct requis
pour cela — `VerifierIntegrerPanel` est un composant unique, les onglets Décaissement (« TVA Déductible »)
et Encaissement (« TVA Collectée ») ne sont qu'un état `selectedTab` qui filtre les mêmes blocs
(`filteredRows`, `RecapCard`, `ChecklistCard`, sous-totaux, `equilibre`) — le réagencement s'applique
mécaniquement aux deux en une seule modification. **Point d'attention VERIFY** : capturer/valider
explicitement les DEUX onglets (pas seulement celui de la capture d'écran d'origine), notamment parce que
`ligneIncoherenteAutreOnglet` (TASK-139) dépend justement du croisement entre les deux onglets — bien
vérifier que le réagencement ne casse pas ce renvoi croisé.

Reste soumis à validation PO avant tout développement.

## Périmètre STRICT (une fois l'option choisie par le PO)
- **Inclus** : réagencement d'affichage dans `VerifierIntegrerPanel.tsx` (composants `RecapCard`,
  `ChecklistCard`, `buildControls`) — retrait des chiffres dupliqués, regroupement visuel des blocs liés
  (item `equilibre` ↔ tableau lignes non valorisées).
- **Exclu** : toute modification de calcul, d'endpoint, ou de source de données (`/checkup`,
  `/declarations/{id}/lignes`) — front seul, aucun changement de comportement métier.
- **Exclu** : le tableau « Sous-totaux par taux TVA » (détail par taux, non redondant) et le bouton
  « Codes activité » (TASK-161, hors sujet).

## Étapes (à affiner une fois l'option PO connue)
1. Recueillir l'arbitrage PO (option A ou B, ou variante).
2. Réagencer `RecapCard`/`ChecklistCard`/`buildControls` selon l'option retenue.
3. Vérifier non-régression des drills existants (TASK-016/107/112/139/144) qui dépendent des callbacks
   `onDrill`/`onDrillIncoherence` — ne pas casser leur câblage en déplaçant l'affichage.
4. Capture d'écran avant/après à joindre au VERIFY.

## Livrables
- `VerifierIntegrerPanel.tsx` réagencé selon l'option validée.
- Capture d'écran avant/après (`VERIFY/TASK-165_verify.md`).

## Critères de validation
- Chaque chiffre (règlements, lignes, total TVA) n'apparaît plus qu'une seule fois en premier niveau de
  lecture.
- Aucune perte d'information : tout ce qui est retiré du premier niveau reste accessible (repli/détail),
  aucune donnée supprimée silencieusement.
- Non-régression des drills (TASK-016/107/112/139/142/144/161) — tous les boutons "Voir lignes"/
  "Diagnostiquer"/"Codes activité" restent fonctionnels.
- Build front 0 erreur.

## Risques / dépendances
- **Bloquant réel : arbitrage PO requis avant tout code** — le choix entre options A/B (ou une variante)
  est un choix de design fonctionnel, pas une décision technique.
- Risque de régression visuelle sur les 6 TASK successives qui ont chacune ajouté un fragment de cet
  écran (TASK-090/018/112/139/142/144/161) — bien vérifier qu'aucun de leurs callbacks/props n'est perdu
  lors du réagencement.
