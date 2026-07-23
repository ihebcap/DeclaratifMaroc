Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\CLAUDE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\CLAUDE.md`, `D:\_vibe\GRF\TODO.md`, puis le fichier
TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Ta mission

Traiter une seule task : `D:\_vibe\GRF\TASKS\TASK-161-code-activite-defaut-tiers-surcharge-ligne.md`
— lis-la **intégralement** avant de commencer, elle contient tout le contexte, les 6 décisions déjà
actées avec le PO, le périmètre strict, l'architecture attendue, les étapes et les critères de
validation. Ne re-débats aucune des 6 décisions listées dans la section « Décisions PO actées en
session » — elles sont tranchées, implémente-les telles quelles.

## Rappel des 6 décisions déjà actées (ne pas réinventer)

1. Ajouter le numéro tiers Sage sur `P_SOCIETECODEACTIVITETIERS` (ALTER additif, nullable,
   `SCAT_ErpIntitule` conservée).
2. **Aucun nouvel écran web GRF** de gestion du référentiel/mapping — GRF web reste en **lecture
   seule** sur `P_DECTVAACTIVITE` et `P_SOCIETECODEACTIVITETIERS`. Le paramétrage continue de se
   faire via l'écran Trésorerie WinForms existant (`apbs-gr_winform`).
3. Si aucun niveau de la cascade ne résout de code activité → **non bloquant**,
   `CodeActivite = ""` / `"(sans activité)"`, jamais d'exception.
4. `P_DECTVASOCTAXEACTIVITE` (mapping taxe→activité) **hors périmètre** — ne pas la lire, ne pas la
   réintroduire sous aucune forme.
5. Le cas "une même facture peut avoir deux codes activité différents" est couvert par une
   **surcharge manuelle directe sur la ligne** (écran ② Vérifier & Intégrer), pas par une table de
   mapping — même pattern que la validation d'incohérence déjà en place (`IncoherenceValidee`/
   `IncoherenceValideePar`/`IncoherenceValideeLe`, TASK-078).
6. Absence d'écran GRF pour peupler le référentiel pour un futur client sans WinForms = **dette
   tracée, non bloquante** — ne rien construire pour ce cas, juste le documenter dans le VERIFY s'il
   ressurgit.

## Règles de travail (non négociables)

- Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis
  écris `VERIFY/TASK-161_verify.md` en suivant le même niveau de détail que
  `DONE_DETAIL/TASK-160_verify.md` (déjà dans le dossier, sers-t'en de modèle : section Périmètre
  livré, Décisions actées en cours de route, Tests, Vérifié indépendamment, Réserves non bloquantes
  documentées non silencieuses). Ne déclare jamais un point validé si tu ne l'as pas réellement
  vérifié.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (base GRF, contient `P_SOCIETE`/`P_DECTVAACTIVITE`/
  `P_SOCIETECODEACTIVITETIERS`/`DM_LGTVA`), `-d "NEW_EMA DISTRIBUTION"` pour la base Sage. Identifiants
  complets dans `D:\_vibe\GRF\connections.json`. **Utilise cet accès pour vérifier réellement la
  cascade de résolution contre les données réelles** (au moins une société `SO_Id` réelle avec un
  mapping tiers existant dans `P_SOCIETECODEACTIVITETIERS`), pas seulement en te fiant à la
  compilation et aux tests unitaires.
- **Périmètre repository STRICT : ne modifie aucun fichier sous `D:\_vibe\apbs-gr_winform`.** Cette
  task vit uniquement dans `D:\_vibe\GRF`. La task documente une dépendance cross-applicatif
  (l'écran WinForms `UcSocieteCodeActiviteTiers` devrait aussi capturer le nouveau numéro tiers pour
  que l'ALTER soit utile) — **ne la traite pas toi-même**, contente-toi de la documenter clairement
  dans le VERIFY comme point d'arbitrage PO restant, exactement comme la task le demande.
- Build back (`dotnet build DeclarationTVA.slnx`) et build front (`cd declaration-tva-web && npx tsc
  -b && npx vite build`) doivent passer **avant** d'écrire le VERIFY. Lance aussi `dotnet test` sur la
  solution complète et compare aux échecs préexistants déjà documentés (TASK-154/155/156/159/160,
  cf. `DONE_DETAIL/TASK-160_verify.md`) — n'importe quel nouvel échec doit être expliqué, jamais
  ignoré.
- **Aucune écriture dans Sage, aucune écriture dans `P_DECTVAACTIVITE`** (lecture seule confirmée par
  toi dans le VERIFY, ex. grep du code livré + relecture des méthodes repository ajoutées).
  `P_DECTVASOCTAXEACTIVITE` : aucune référence dans le code livré (vérifie par grep avant de clore).
- Pas de JOIN SQL trois-parties (garde-fou TASK-154) — toute jointure entre bases/tables distinctes se
  fait en mémoire, par lots.
- Jamais de valeur de code activité inventée — `""` explicite si non résolu (cf. décision 3).
- Tests attendus (cf. section « Étapes »/« Critères de validation » de la task) : les 4 niveaux de la
  cascade isolément (surcharge ligne > tiers > Sage > vide) ; cas confirmé PO — une facture avec 2
  lignes de taux différents, l'une modifiée manuellement vers une autre activité ; non-régression
  TASK-160 (le recap par activité de l'export de contrôle doit refléter des valeurs réelles, plus le
  bucket `""` systématique) ; persistance après figeage stable malgré un changement ultérieur du
  mapping tiers ; non-régression `apbs-gr_winform` (aucune requête WinForms existante cassée par
  l'ALTER additif — relis `SocieteCodeActiviteTiersRepository1.cs` pour confirmer qu'aucune colonne
  n'est retirée/renommée).
- Ne touche à aucun fichier hors du périmètre de la task (`Positionnement / architecture` +
  `Livrables`). Pas de refactor, pas de nettoyage non demandé.
- Committe le travail **en un seul commit** à la fin (message clair décrivant le correctif), jamais de
  `--no-verify`, **ne jamais pousser (`git push`)** — le commit reste local pour revue.
- Si un point est réellement ambigu ou nécessite une décision métier que tu ne peux pas prendre seul
  (au-delà des 6 déjà tranchées) : **arrête-toi sur ce point précis, documente-le clairement dans le
  VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse le VERIFY dans `VERIFY/TASK-161_verify.md` (ne le déplace pas toi-même vers `DONE_DETAIL/` —
c'est le rôle de l'architecte de le reviewer et de faire la clôture). Termine par un résumé indiquant
clairement : build back/front OK ou KO, nombre de tests ajoutés/passés, et la liste des points laissés
ouverts (dépendance cross-applicatif WinForms notamment) pour la revue architecte.
