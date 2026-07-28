# TASK-029 — Rendre le guide fonctionnel accessible depuis l'application

## Contexte
Un **guide fonctionnel** (version client) décrivant le process de déclaration de TVA déductible a été produit :
`DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` — fichier HTML **autonome** (CSS inline, thème clair/sombre,
imprimable, aucune dépendance externe). Il doit être **consultable directement depuis le front**
`declaration-tva-web` (React 19 / Vite / TS), sans que l'utilisateur ait à ouvrir un fichier hors application.

> ✅ **Guide rafraîchi en v2 (28/07/2026)**, à la demande du PO (« la partie TVA est plus au moins stable,
> on peut mettre à jour ») — contenu revu par l'architecte contre tout ce qui a été livré depuis la v1
> (08/07/2026) : le modèle métier de fond (4 interrogations, affectation, statuts, calcul, origines,
> dépôt DGI) n'a pas changé, confirmant le constat de stabilité. Deux ajouts réels : (1) nouvelle section
> « 5 · Code activité » (jusqu'ici absente), décrivant le référentiel par domaine (Achats/Ventes disjoints,
> TASK-172), la résolution automatique par le tiers ERP avec repli explicite sur vide (jamais de défaut
> silencieux, TASK-179), l'affectation manuelle unitaire/en masse (TASK-173), et la ventilation à 3 axes
> source/taux/code activité (TASK-174/184/185) ; (2) note sur la **réouverture d'une déclaration clôturée
> par un profil administrateur** (§6, TASK-095), absente de la v1. Le module « Délai de Paiement » (DDP,
> TASK-127-136) est un périmètre **distinct**, non couvert par ce guide. Voir le fichier HTML pour le
> détail — **la source de vérité reste ce fichier `DOCS/`**, à copier tel quel dans `public/` (étape 1
> ci-dessous), non réécrit.
>
> ⚠️ **Correction factuelle supplémentaire (28/07/2026, signalée par le PO : « Exclue ça n'existe pas
> maintenant »)** — vérification code demandée et faite avant de rectifier. Confirmé par grep exhaustif
> (`Declaration.Application/Services/DeclarationWorkflowService.cs`) : `EtatLigne.Exclue`/`Reportee`/
> `Ecartee` ne sont **plus jamais assignés** depuis TASK-097/099 (décision PO 14/07/2026, déjà livrée)
> — seuls `Proposee`/`Integree` sont produits aujourd'hui ; le geste « je ne déclare pas » est
> simplement **ne pas cocher** un règlement à l'étape Sélection (aucun motif, aucun statut créé,
> resélectionnable sans limite de temps). Le composant portant l'ancien bouton « Exclure »
> (`WorkstationPanel.tsx`) est confirmé **mort** — plus aucun `import`/`<WorkstationPanel` dans le
> front. §6 (Statuts) réécrite en conséquence (2 états au lieu de 5), §4 (4ᵉ interrogation) et §7
> (dates — la borne basse « dans le mois » a aussi été supprimée par TASK-099, corrigée en écho) mis
> à jour en cohérence. Les 3 statuts legacy restent lisibles en base sur des déclarations figées
> avant TASK-097 (non rétroactif) mais ne sont plus produits pour les nouvelles.

État front vérifié (08/07/2026) :
- `declaration-tva-web/public/` existe (sert les assets statiques à la racine ; copiés dans `dist/` au build).
- `src/App.tsx` porte une **sidebar** (`.sidebar-menu`, items `.sidebar-item`) avec les entrées « Mes Déclarations »
  et « Déclaration en cours », icônes `lucide-react`.

## Objectif
Ajouter une entrée **« Guide »** (aide) dans la navigation, qui ouvre le guide fonctionnel. Le guide est servi
comme **asset statique** du front pour rester disponible en production (build Vite) sans back dédié.

## Périmètre STRICT
- **Front `declaration-tva-web` uniquement** + copie du fichier HTML dans `public/`.
- **Aucune** modification back / API / calcul / persistance.
- Ne pas réécrire le contenu du guide : le fichier HTML est la **source unique** (voir « Synchronisation »).
- Pas de nouvelle dépendance UI ; réutiliser `lucide-react` (déjà présent) pour l'icône.

## Étapes
1. **Copier** `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` vers
   `declaration-tva-web/public/guide-fonctionnel-tva.html` (asset statique servi à `/guide-fonctionnel-tva.html`).
2. Dans `src/App.tsx`, ajouter un **`sidebar-item` « Guide »** (icône `BookOpen` ou `HelpCircle` de `lucide-react`),
   cohérent avec les items existants (title au survol, comportement sidebar repliée/dépliée).
3. Au clic : ouvrir le guide dans un **nouvel onglet** — `window.open('/guide-fonctionnel-tva.html', '_blank',
   'noopener')`. (Respecte le `base` Vite si configuré : utiliser `import.meta.env.BASE_URL` pour préfixer l'URL.)
4. Vérifier le rendu **repliée / dépliée** de la sidebar et l'accessibilité (focus clavier, `aria-label`/`title`).
5. Build `tsc -b && vite build` + `oxlint` : 0 erreur ; confirmer que
   `dist/guide-fonctionnel-tva.html` est bien produit.
6. Test e2e Playwright : depuis le dashboard connecté, cliquer « Guide » → un nouvel onglet s'ouvre sur le guide
   (vérifier l'URL et la présence du titre « Déclaration de TVA déductible »).

## Synchronisation (source unique)
- La **source de vérité** du contenu reste `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`. La copie dans `public/`
  en est un **miroir**. Documenter dans le README front la commande de copie et **quand** la rejouer
  (à chaque mise à jour du guide). Ne pas diverger les deux fichiers.
- (Optionnel, non bloquant) automatiser la copie via un script `predev`/`prebuild` dans `package.json`
  pour éviter l'oubli — à évaluer sans complexifier le build.

## Contraintes techniques
- React 19 + Vite + TS ; `oxlint` ; e2e Playwright. `API_BASE` runtime + JWT inchangés.
- Le guide est **autonome** : ne pas l'intégrer via un composant React (pas de ré-implémentation), le servir tel quel.
- **Simple avant beau** : une entrée de menu + ouverture nouvel onglet. Pas de viewer intégré ni de modale lourde
  en premier jet (une iframe/modale peut être une amélioration ultérieure si le PO le demande).

## Livrables
- `declaration-tva-web/public/guide-fonctionnel-tva.html` (miroir du guide).
- Entrée « Guide » dans la sidebar de `src/App.tsx`, ouverture du guide en nouvel onglet.
- README front : section « Guide fonctionnel » (emplacement source, procédure de synchronisation).
- Test e2e Playwright du parcours d'ouverture.
- `VERIFY/TASK-029_verify.md` : capture de l'entrée de menu (sidebar repliée + dépliée), capture du guide ouvert,
  log build (`dist/guide-fonctionnel-tva.html` présent), log e2e vert.

## Critères de validation
- Depuis l'application connectée, une entrée **« Guide »** est visible dans la navigation et ouvre le guide
  fonctionnel (nouvel onglet), en dev **et** après `vite build` (asset présent dans `dist/`).
- Le contenu affiché est **identique** à `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` (miroir, pas de divergence).
- `oxlint` + build tsc/vite OK ; parcours e2e Playwright vert.
- Aucun changement back / API / calcul.

## Risques / dépendances
- **Découplé** des chemins critiques (021 → 019 ; 022/025 → 009). Peut être fait en parallèle.
- **Divergence de contenu** : risque principal = la copie `public/` se désynchronise de la source `DOCS/`.
  Mitigé par la doc README (+ script `prebuild` optionnel).
- Si un `base` Vite non-racine est configuré (sous-chemin de déploiement), l'URL d'ouverture doit passer par
  `import.meta.env.BASE_URL` — sinon lien cassé en production.
