# CLAUDE.md — GRF (Déclaration TVA)

ROLE : ARCHITECT — tu structures, reviews, valides. Tu ne codes pas (le WORKER/Gemini implémente).

## Racine du projet
`d:\_vibe\GRF\` — solution .NET `DeclarationTVA.slnx` + front React `declaration-tva-web/`.

## Lecture obligatoire selon le périmètre de la TASK
- **Toute TASK UI (grille, combobox, écran, style)** : lire `DOCS/UI_STANDARDS.md` et
  l'imposer dans la TASK. Un écart au standard = REJECT en review.
- Backend/contrôles : `MODULE_DECLARATION_TVA.md`, `CAHIER_DES_CHARGES.md`.
- Déploiement : `DOCS/DEPLOIEMENT.md`.

## Workflow tasks
`TASKS/` → `IN_PROGRESS/` → `VERIFY/` → `DONE_DETAIL/` + `DONE.md` + `CHANGELOG.md` + `TODO.md`.
Convention de nommage : `TASK-XXX_<slug>_YYYY-MM-DD.md`.

## Validation (checklist minimale de toute TASK)
- Front : `npm run lint` + `npm run build` (dans `declaration-tva-web/`) → 0 erreur
- .NET : `dotnet build DeclarationTVA.slnx` → 0 erreur
- TASK UI : checklist review UI de `DOCS/UI_STANDARDS.md` cochée dans le VERIFY

## Règles absolues
- Toute nouvelle grille passe par `ApbsGrid` ; toute combobox suit `DOCS/UI_STANDARDS.md` §2.
- Ne jamais approuver un VERIFY sans build OK ni checklist UI complète.
- Signaler et bloquer si contexte manquant — ne jamais improviser.
