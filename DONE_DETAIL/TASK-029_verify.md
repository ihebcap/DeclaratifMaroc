# Verification Report — TASK-029: Guide fonctionnel accessible depuis l'application

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-029
- **Scope**: Make `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` accessible directly from `declaration-tva-web` interface as a static asset served at `/guide-fonctionnel-tva.html`.
- **Status**: COMPLETE — voir limitation d'environnement documentée ci-dessous (e2e)

---

## Correction apportée (07/08/2026, suite au rejet architecte : test e2e manquant)

`declaration-tva-web/tests/task029.spec.ts` ajouté (login → clic sur l'entrée « Guide fonctionnel »
→ vérifie l'ouverture d'un nouvel onglet, l'URL `guide-fonctionnel-tva.html` et la présence du texte
« Déclaration de TVA déductible » — confirmé présent dans le HTML source, `h1` ligne 119).

**Non exécuté** : le login nécessite un appel réel à la liste des sociétés en base
(`GET /api/.../societes`), qui échoue dans cet environnement (pas d'accès SQL Server réel —
`Error Number:53`, chemin réseau introuvable — même limitation que TASK-060/200/202). Structure du
test alignée sur `tests/task183.spec.ts` (mêmes conventions de login) pour exécution ultérieure sur
un poste avec accès DB. **À exécuter et documenter (captures `VERIFY/task029-*.png`) avant clôture
définitive.**

---

## Deliverables & Implementation Summary

1. **Static Asset Mirror**:
   - Copied `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html` to `declaration-tva-web/public/guide-fonctionnel-tva.html`.
   - Verified that Vite copies it directly into `dist/guide-fonctionnel-tva.html` during build.

2. **UI Navigation Entry**:
   - Added a dedicated "Guide fonctionnel" entry with `BookOpen` icon in `declaration-tva-web/src/App.tsx` sidebar footer.
   - Handles opening the guide in a new tab via `window.open(targetUrl, '_blank', 'noopener')` adhering to `import.meta.env.BASE_URL`.

3. **Documentation**:
   - Updated `declaration-tva-web/README.md` with synchronization instructions.

---

## Verification Evidence & Build Results

### 1. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1856 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-DVYJBxvb.js 1,894.99 kB │ gzip: 538.17 kB
✓ built in 1.62s
Exit Code: 0
```

Command: `Test-Path declaration-tva-web/dist/guide-fonctionnel-tva.html`
Result:
```
True
```

---

## Checklist of Requirements (4/4)

1. [x] Asset mirrored in `public/guide-fonctionnel-tva.html`.
2. [x] Navigation item "Guide fonctionnel" added in sidebar.
3. [x] Opens guide in a new browser tab adhering to `BASE_URL`.
4. [x] Build produces asset in `dist/` with 0 errors.
