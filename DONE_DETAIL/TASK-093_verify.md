# VERIFY — TASK-093 — Renommage titre page d'authentification (« Déclaratif Maroc »)

## Résumé
Changement de texte pur, front-only. Le titre de la page de connexion et le `<title>` de l'onglet passent de « Déclaration TVA » / « declaration-tva-web » à **« Déclaratif Maroc »**. Aucune logique touchée.

## Modifications réalisées
| Fichier | Ligne | Avant | Après |
|---|---|---|---|
| `declaration-tva-web/src/Auth.tsx` | 79 | `<h1 className="auth-title">Déclaration TVA</h1>` | `<h1 className="auth-title">Déclaratif Maroc</h1>` |
| `declaration-tva-web/index.html` | 7 | `<title>declaration-tva-web</title>` | `<title>Déclaratif Maroc</title>` |

Sous-titre (`Auth.tsx:80`) conservé tel quel (générique). Sidebar post-connexion (`App.tsx:133`, `<span>TVA</span>`) **non touchée** — nom du module actif, hors périmètre.

## Vérifications
- **Étape 3 — grep `Déclaration TVA`** sur `Auth.tsx` + `index.html` : **0 occurrence** après correctif. ✅
- **Étape 4 — build front** (`npm run build` → `tsc -b && vite build`) : **✓ built in 659ms, 0 erreur**. ✅
  - Seul message : warning Vite `[INEFFECTIVE_DYNAMIC_IMPORT]` sur `src/api.ts`, préexistant et sans lien avec ce changement (import dynamique dans `Auth.tsx` aussi importé statiquement ailleurs).

## Critères de validation
- [x] Page d'authentification affiche « Déclaratif Maroc » (source `Auth.tsx:79`).
- [x] Onglet navigateur affiche « Déclaratif Maroc » (`index.html:7`).
- [x] Sidebar post-connexion affiche toujours « TVA » (non régressée, non touchée).
- [x] Aucun changement de logique d'authentification (`handleSubmit`, `/auth/login`, sélection société) — texte seul.
- [x] Build front 0 erreur.

## Note renumérotation
Renumérotée **082 → 093** à l'archivage : le n° 082 était déjà pris par une autre tâche livrée (bandeau incohérence ligne `Exclue`, DONE.md/CHANGELOG). Aucun impact code.

## Preuve visuelle PO
Capture PO (14/07/2026) sur `localhost:5173` confirmant :
- Onglet navigateur : **« Déclaratif Maroc »** ✅
- Titre de la carte de connexion : **« Déclaratif Maroc »** ✅
- Sous-titre inchangé « Accès sécurisé à l'espace de gestion », formulaire (utilisateur / mot de passe / société / Se Connecter) inchangé ✅

## Statut
APPROUVÉE (2026-07-14)
