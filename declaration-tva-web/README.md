# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.

## Guide fonctionnel TVA

Le guide fonctionnel (version client) est stocké à la racine du projet sous `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`.

Pour le rendre accessible directement depuis l'application front :
- Le fichier est copié vers `declaration-tva-web/public/guide-fonctionnel-tva.html`.
- En production/build Vite, cet asset statique est servi sous `/guide-fonctionnel-tva.html`.
- Procédure de synchronisation à rejouer lors de chaque mise à jour de la documentation :
  ```powershell
  Copy-Item -Path ../DOCS/GUIDE_PROCESS_DECLARATION_TVA.html -Destination public/guide-fonctionnel-tva.html
  ```
