# VERIFY — TASK-168 : filtre « N° Facture »/« Référence » tronqué à 500 valeurs, recherche uniquement locale

## Contexte
Suite directe de l'investigation `FC2600515` (TASK-164) : le filtre `N° Facture`/« Référence » de
l'écran Factures ne cherchait que dans les 500 premières valeurs alphabétiques renvoyées par
`/factures/distincts` (`DistinctsTopBound`), sans jamais interroger le serveur au-delà. Cette task
rend la recherche exhaustive, sans retirer le plafond par défaut (garde-fou anti-charge).

## Ce qui est livré

### Back-end (lecture seule stricte, additif)
- `IDeclarationRepository.GetFacturesInterrogationDistinctsAsync` : deux nouveaux paramètres
  optionnels `rechercheNumero`/`rechercheReference` (défaut `null`, aucune régression de signature
  pour un appel existant).
- `DeclarationRepository.GetFacturesInterrogationDistinctsAsync` : le `LIKE @recherche + '%'`
  (préfixe — choix documenté ci-dessous) est appliqué **avant** le `TOP {DistinctsTopBound}`, sur le
  MÊME `FacturesFromWhere`/`FacturesParams` que la liste principale (invariant TASK-040/067B
  inchangé — aucun autre filtre du WHERE modifié).
- `FactureInterrogationDistincts` : deux nouveaux booléens `NumerosTronque`/`ReferencesTronque`
  (true uniquement quand la vue **par défaut**, sans recherche, atteint exactement le plafond) —
  jamais de troncature silencieuse (Objectif §4 de la task).
- `FacturesController.GetDistincts` : expose `rechercheNumero`/`rechercheReference` en query params
  et `numeroTronque`/`referenceTronque` dans la réponse JSON.
- 13 fakes de test (`Declaration.Orchestration.Tests`, pattern signalé par TASK-147) mis à jour avec
  la nouvelle signature (paramètres optionnels ajoutés, comportement `NotImplementedException`/
  `NotUsed()` inchangé — aucun de ces fakes n'exerce ce chemin).

### Choix d'arbitrage mineur (documenté, non tranché explicitement par le PO)
Recherche par **préfixe** (`LIKE @x + '%'`) retenue plutôt que sous-chaîne (`LIKE '%' + @x + '%'`) :
compatible index sur `DO_Numero`/`DO_Reference`, cohérent avec le tri alphabétique déjà utilisé pour
le picklist par défaut. Si le PO constate qu'un numéro réel ne peut être retrouvé que par une
sous-chaîne (ex. suffixe), ce choix serait à revoir — non observé pendant la vérification réelle
ci-dessous (le cas `FC2600515` est bien retrouvé par préfixe, comme tout numéro de facture réel
observé dans ce jeu de données, tous commençant par leur préfixe significatif).

### Front (`ExcelFilter.tsx`, `FactureInterrogation.tsx`)
- `ExcelFilter` : nouvelle prop optionnelle `onRemoteSearch` — dès que l'utilisateur tape ≥2
  caractères dans la recherche (filterType `list`), un appel debouncé (300 ms) est déclenché en plus
  du filtrage local existant (`filteredOptions` inchangé, aucune régression). Nouvelle prop
  `remoteTruncated` : affiche un message explicite (« 500+ valeurs sur cette période — tapez au
  moins 2 caractères… ») quand la vue par défaut est tronquée — jamais silencieux. `remoteLoading` :
  spinner discret pendant la recherche serveur.
- `FactureInterrogation.tsx` : `handleRemoteSearchNumero`/`handleRemoteSearchReference` appellent
  `/factures/distincts` avec le terme tapé et **fusionnent** (union, dédoublonnée, triée) le résultat
  avec les options déjà chargées — jamais de remplacement destructif de la liste existante, même en
  cas d'échec réseau (catch silencieux, liste inchangée).
- Colonnes `origine`/`statut` (domaine fixe/borné) non concernées — aucune prop remote ajoutée,
  comportement strictement inchangé.

### Hors périmètre (documenté, non traité)
- L'équivalent Rapprochement (`RapprochementFromWhere`, `M.MV_Numero`/`M.MV_ExtraitNum`/
  `B.BanqueCode`) n'a **pas** été touché — la task le classait explicitement "non vérifié si aussi
  impacté en pratique... à couvrir... si le volume le justifie", pas un critère de validation ferme.
  Aucune mesure de volume réel effectuée sur ce chemin pendant cette session (hors périmètre strict).

## Vérification indépendante des critères de validation

- [x] **Build back + front OK** — `dotnet build DeclarationTVA.slnx` : 0 erreur. `npx tsc --noEmit` :
      0 erreur. `npm run build` : succès (bundle généré, avertissement `INEFFECTIVE_DYNAMIC_IMPORT`
      préexistant sans rapport avec cette task).
- [x] **Sur une période réelle avec >500 numéros distincts, taper un numéro situé après le 500ᵉ
      alphabétique le fait apparaître** — preuve réelle sur `GR_EMA_DISTRIBUTION` (soId=1, instance
      de dev port 5299, jamais :5280) :
      - `GET /factures/distincts?debut=2026-01-01&fin=2026-12-31&soId=1` (sans recherche) →
        `numero.length=500`, `numeroTronque=true`, dernière valeur alphabétique `FC2600497`.
      - `GET /factures/distincts?...&rechercheNumero=FC2600515` → `numero=["FC2600515"]` — ce numéro
        est bien postérieur alphabétiquement à `FC2600497` (`"497" < "515"` caractère par caractère)
        et donc absent de la vue par défaut ; il est retrouvé exclusivement grâce au nouveau
        paramètre de recherche serveur. Preuve brute conservée dans les logs de session (curl direct,
        JWT réel obtenu via `POST /api/auth/login` Admin/Admin).
- [x] **Aucune régression sur le comportement par défaut** — même appel sans recherche : toujours
      500 valeurs triées alphabétiquement (identique à avant la task), `WHERE`/`FacturesParams`
      inchangés (aucun autre filtre touché), invariant TASK-040/067B préservé (mêmes `FromWhere`
      pour liste principale/COUNT/distincts).
- [x] **Tests existants rejoués verts** — `dotnet test DeclarationTVA.slnx` :
      `Declaration.Orchestration.Tests` 176/176 (inclut le nouveau test
      `FactureInterrogationDistincts_Task168_TronqueFauxParDefaut`), `Declaration.Core.Tests` 54/54,
      `Declaration.Selection.Tests` 59/59, `Declaration.Export.Xml.Tests` 13/13,
      `Declaration.Export.Excel.Tests` 3/3. Un seul échec, **préexistant et sans rapport** :
      `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification` (« Déclaration GRFN 66
      introuvable » — test legacy dépendant d'un état de base spécifique non présent sur ce poste,
      aucun fichier touché par cette task n'est dans ce chemin).

## Réserves non bloquantes
- Test de non-régression du composant `ExcelFilter`/`FactureInterrogation` non rejoué en navigateur
  réel (aucun outil d'automatisation navigateur disponible dans cette session) — uniquement `tsc`
  (typage) + build Vite (compilation) + preuve API directe (curl). Le rendu visuel du hint de
  troncature et le déclenchement effectif du debounce au clavier n'ont donc pas été observés
  visuellement ; la logique (debounce 300 ms, seuil 2 caractères, fusion des options) est cependant
  directement lisible dans le code livré.
- Rapprochement (colonnes identifiantes équivalentes) non couvert — cf. « Hors périmètre » ci-dessus.
