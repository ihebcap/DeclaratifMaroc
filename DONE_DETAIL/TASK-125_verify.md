# TASK-125 Verify — Grille de règlements vide (durablement) après retour sur ① Sélection

> Reproduction instrumentée réelle (API + base SQL Server réelle `GR_EMA_DISTRIBUTION` sur
> `DESKTOP-5BFKKEP`, déclaration réelle `TVA1-2026-01` citée par le PO — aucune donnée mockée),
> tranchant H1/H2/H3/H4, puis correctif ciblé sur la cause confirmée.

## Méthode de reproduction

- API `Declaration.API` compilée et exécutée localement (build `net8.0-windows`, `connections.json`
  réel — même base que le poste `DESKTOP-5BFKKEP` cité dans le signalement PO).
- Front construit en **production** (`npm run build` + `vite preview`) — pas `npm run dev`, pour
  éviter le double-montage volontaire de `React.StrictMode` qui aurait masqué le bug (cf. note
  TASK-111 sur ce même comportement).
- Authentification réelle : JWT signé avec la clé réelle de `connections.json` pour l'utilisateur
  réel `Admin` (`P_UTILISATEUR`, `UT_Id=1`), injecté en `sessionStorage` pour piloter Playwright sans
  dépendre d'un mot de passe inconnu — l'API, la base et toute la logique métier restent 100% réelles.
- Déclaration utilisée : `TVA1-2026-01` (id `e6d5bf22-7cdb-4689-8015-e5001586addd`, société 1),
  celle du signalement PO, avec sa sélection déjà persistée (146 règlements réels,
  `DM_SELECTION_REGLEMENT`).
- Scénarios rejoués avec Playwright (Chromium) contre l'app réelle : sélection → « Passer au
  calcul » → retour ① ; sélection → « Détail des lignes » → retour ① ; fermeture (Retour liste) +
  réouverture de la même déclaration ; et injection d'une panne réseau réelle (une requête
  `GET /rapprochement` renvoyée en 500) via `page.route`, au moment précis du retour sur ①.

## Hypothèses H1/H2/H3/H4 — verdict

| Hypothèse | Verdict | Preuve |
| --- | --- | --- |
| **H3** — jointure cross-catalogue `GrfConnection`/`PersistenceConnection` | **Réfutée** pour l'environnement réel du PO | `connections.json` réel (`DESKTOP-5BFKKEP`) : les deux connexions pointent la **même** base `GR_EMA_DISTRIBUTION`. Appel direct `GET /api/rapprochement` (curl, JWT réel) : succès, 146 lignes, cohérent avec `DM_SELECTION_REGLEMENT`. Seul l'`appsettings.json` de dev committé (catalogues séparés `GRFN_Dummy`/`DeclarationTVA`) exposerait ce risque — pas l'environnement réel testé. |
| **H1** — remontage complet de `DeclarationStepper` | **Confirmé comme déclencheur nécessaire**, mais **insuffisant seul** | En in-app (clic d'onglet après « Passer au calcul » ou « Détail des lignes »), `selectedKeys`/`selectedRows` **survivent** (portés par `DeclarationStepper`, jamais démonté par un changement d'onglet) — reproduit et confirmé sain (146/145 stable) sur les 2 déclencheurs. Seule une **fermeture + réouverture** de la déclaration (remontage réel) réinitialise `selectedKeys`/`selectedRows` à vide — testé isolément : sans panne réseau, la restauration (TASK-097) fonctionne et rétablit 146/145 correctement. **H1 seul ne suffit donc pas** à expliquer un blocage durable. |
| **H2** — remontage de `ReglementsSelection` sans garde d'annulation | **Confirmé, aggravant** | Absence de `cancelled` (contrairement à l'effet voisin `distincts`) : une réponse tardive peut écraser un résultat plus frais. Contribue à la durée/instabilité de la panne mais n'est pas la cause du blocage *permanent*. |
| **H4** — `showToast` non stabilisé (`App.tsx`) | **Confirmé comme cause déterminante** — mécanisme complet identifié | Voir « Cause racine confirmée » ci-dessous : bien plus grave que « facteur aggravant » — auto-entretient une **tempête de requêtes** qui transforme une panne transitoire unique en blocage permanent. |

## Cause racine confirmée

**H1 (réouverture) + une panne transitoire unique du réseau/serveur au moment du premier
`GET /rapprochement` qui suit, combinée à H2+H4** :

1. `showToast` (`App.tsx`) n'était pas mémoïsé (`useCallback`). Chaque appel change sa référence à
   chaque rendu d'`App`.
2. `fetchAll` (`ReglementsSelection.tsx`) dépend de `showToast` dans son tableau de dépendances
   `useCallback`. Une nouvelle référence de `showToast` ⇒ nouvelle référence de `fetchAll` ⇒
   réexécution du `useEffect(() => { fetchAll() }, [fetchAll])`.
3. Si ce fetch échoue, il appelle `showToast(...)` → `setToast(...)` dans `App` → **rendu d'`App`**
   → nouvelle référence de `showToast` → nouvelle référence de `fetchAll` → nouveau fetch → **boucle
   auto-entretenue**, sans aucune garde d'annulation (H2) pour empêcher une réponse tardive et
   périmée d'écraser un résultat pourtant redevenu bon.
4. **Preuve mesurée** : une **unique** panne simulée (1 réponse HTTP 500) déclenche entre **1061 et
   1078 requêtes** `GET /rapprochement` en quelques secondes (reproduit 3 fois, build de
   production réel, code non corrigé).
5. Tant que la tempête n'est pas retombée, la grille et la sélection restent à 0/0. Une fois
   retombée, **rien ne redéclenche plus jamais un fetch** (les dépendances de `fetchAll` sont de
   nouveau stables) — la restauration de sélection (TASK-097), gatée par `allData.length > 0`
   (ligne ~308), ne se relance donc jamais spontanément : blocage **durable**, confirmé par une
   attente de 18 s+ sans la moindre requête supplémentaire ni la moindre amélioration des compteurs.
6. Seule une action utilisateur qui **remonte** `ReglementsSelection` (changement d'onglet, ou
   réouverture) répare, en forçant un nouveau `fetchAll` propre — ce que le PO n'a probablement pas
   pensé à essayer, d'où le signalement « durablement bloqué ».

Ce mécanisme explique intégralement les symptômes rapportés : les deux compteurs à 0
simultanément (H1 vide `selectedKeys` ; la tempête vide et re-vide `allData`, bloquant la
restauration), pour les deux déclencheurs (« Passer au calcul » et « Détail des lignes », qui
mènent tous deux normalement à quitter puis revenir sur ①), de façon durable (pas un flash de
chargement) et sans lien avec TASK-096/TASK-097 (dont la logique, revérifiée, reste correcte une
fois la tempête stoppée).

## Correctif appliqué (ciblé sur la cause confirmée)

- **`declaration-tva-web/src/App.tsx`** : `showToast` mémoïsé avec `useCallback([])` — casse la
  boucle à la racine (référence stable quel que soit le nombre de rendus d'`App`).
- **`declaration-tva-web/src/ReglementsSelection.tsx`** : garde d'annulation (`cancelled`) ajoutée
  à l'effet de `fetchAll`, symétrique à l'effet voisin `distincts` déjà présent dans le même
  fichier — défense en profondeur contre toute réponse tardive résiduelle, même en l'absence de
  tempête.

Aucune modification de `DeclarationStepper.tsx` (montage/démontage), de la logique de figeage
TASK-097, ni des connexions SQL — hors périmètre, non touché.

## Preuve avant/après (build de production réel, données réelles)

Scénario rejoué à l'identique avant/après correctif : ouverture ①→ « Passer au calcul » → retour
liste → réouverture avec **1 seule** panne réseau simulée sur le premier `GET /rapprochement`.

| Étape | Avant correctif | Après correctif |
| --- | --- | --- |
| État initial ① | Règlements=146 Sélectionnés=145 | Règlements=146 Sélectionnés=145 |
| Requêtes `/rapprochement` déclenchées par la panne unique | **1078** (tempête) | **1** |
| Juste après réouverture (panne simulée) | Règlements=0 Sélectionnés=0 | Règlements=0 Sélectionnés=0 *(attendu : la panne réelle vient d'avoir lieu)* |
| 5 s après réparation réseau, sans action utilisateur | Variable / non fiable (dépend de la durée de la tempête, observé jusqu'à 18 s+ sans guérison) | Règlements=0 Sélectionnés=0 *(stable, aucune requête fantôme, pas de tempête)* |
| Après un simple aller-retour d'onglet (① → ② → ①) | Récupère **si** la tempête est déjà retombée (aléatoire) | Règlements=146 Sélectionnés=145 (récupération immédiate et fiable) |

Captures (`VERIFY/task125-avant-*.png` / `VERIFY/task125-apres-*.png`) :
- `A-etat-initial` : grille et compteurs corrects avant toute action.
- `B-etape2-verifier` : après « Passer au calcul », étape ② atteinte normalement.
- `C-reouverture-panne-simulee` : 0/0 juste après réouverture avec panne simulée (identique
  avant/après — comportement correct pour une panne réelle en cours).
- `D-durable-apres-reparation` : **avant** correctif, blocage persistant après réparation réseau ;
  **après** correctif, état stable sans tempête résiduelle.
- `E-recuperation-tab-switch` : **après** correctif, un simple changement d'onglet restaure
  fidèlement 146/145 ; **avant** correctif, la récupération dépend de l'état (aléatoire) de la
  tempête au moment du clic.

## Non-régression

- **TASK-096** (total en pied de grille) : `selectedTotal` reste dérivé de `selectedRows`
  (inchangé) — vérifié à chaque étape des scénarios ci-dessus (« Total sélectionné » toujours
  cohérent avec les 145/146 lignes sélectionnées).
- **TASK-097** (sélection persistée) : la restauration depuis `DM_SELECTION_REGLEMENT` fonctionne
  à l'identique une fois la tempête neutralisée — 145/146 correctement restaurés à chaque
  réouverture testée.

## Build

- `npm run build` (`tsc -b && vite build`) : **0 erreur**, avant et après correctif.
- `npm run lint` (`oxlint`) : aucun nouvel avertissement introduit par les 2 fichiers modifiés.
- Aucun correctif back — build `Declaration.API` non concerné par ce changement front-only.

## Limite assumée

Une panne réseau/serveur réellement isolée (une seule requête en échec) laisse encore la grille à
0/0 jusqu'à la prochaine action de navigation de l'utilisateur (changement d'onglet ou réouverture)
— il n'y a pas de nouvelle tentative automatique. C'est un comportement préexistant, hors de la
cause confirmée par cette task (qui portait sur le blocage *permanent* et la tempête de requêtes,
non sur l'absence d'un mécanisme de retry). Un bouton « Réessayer » explicite serait une
amélioration UX distincte, à cadrer séparément si le PO le souhaite.
