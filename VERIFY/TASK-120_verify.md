# VERIFY — TASK-120 — Nouvelle teinte signature « noir + vert » (variables CSS cœur + sidebar)

> Implémenté par l'assistant en **implémenteur exceptionnel** cette nuit (18→19/07/2026), sur
> autorisation explicite du PO donnée en session (dérogation ponctuelle au rôle architecte/review de
> `CLAUDE.md`, "je dors, personne ne répondra"). Ce document est soumis pour revue PO/architecte le
> matin, **pas auto-approuvé** — cf. `TODO.md`/clôture officielle réservée à l'architecte.

## Résumé
Périmètre strictement respecté : `index.css` (7 variables), `index.html` (favicon), `App.tsx`
(icône sidebar). Aucun fichier backend/.csproj touché. Aucun changement de layout/structure.

## Modifications
- `declaration-tva-web/src/index.css` (`:root`) : les 7 variables remplacées avec les valeurs
  **exactes** du tableau "Palette proposée" de TASK-120 (aucune teinte inventée) :
  - `--accent-primary`: `#2b4c7e` → `#178a4c`
  - `--accent-secondary`: `#3d6099` → `#2fa868`
  - `--accent-hover`: `#1f3a63` → `#0f6b3a`
  - `--sidebar-bg`: `#ffffff` → `#1a1a1a`
  - `--sidebar-text`: `#4a4a4a` → `#e0e0e0`
  - `--sidebar-active-bg`: `#e8ecf3` → `#0f3d24`
  - `--sidebar-active-text`: `#2b4c7e` → `#3ddc84`
  - `--bg-*`, `--text-*`, `--border-color`, `--danger`, `--success`, `--warning`, ombres/radius :
    **inchangés** (conforme au périmètre strict).
- `declaration-tva-web/public/` : copie telle quelle des 4 fichiers livrés par l'architecte dans
  `TASKS/assets/task-120-icon-dm/` (`icon-dm.svg`, `icon-dm-32.png`, `icon-dm-192.png`,
  `icon-dm-512.png`) — aucun fichier régénéré/redessiné.
- `declaration-tva-web/index.html` : ajout de 4 balises (`icon` svg, `icon` png 32×32, `icon` png
  192×192, `apple-touch-icon`), remplaçant l'ancien `<link rel="icon" href="/favicon.svg">`.
  L'ancien `public/favicon.svg` n'a pas été supprimé (non demandé explicitement par la task — laissé
  en place, devenu un fichier orphelin ; signalé ici plutôt que supprimé silencieusement).
- `declaration-tva-web/src/App.tsx` (ligne ~223) : `<span>DM</span>` remplacé par
  `<img src="/icon-dm.svg" alt="DM" width={18} height={18} style={{borderRadius:'4px',
  imageRendering:'pixelated'}} />`, même emplacement (avant le libellé "TVA"), taille ~18px
  cohérente avec l'icône `LayoutDashboard` voisine (22px). Aucune autre occurrence de `DM` en dur
  trouvée dans `App.tsx` (grep de contrôle, un seul emplacement existait).

## Grep de contrôle — 0 résidu ancienne teinte
```
grep -rn "#2b4c7e|#3d6099|#1f3a63|#e8ecf3" declaration-tva-web/src/
```
→ **0 résultat.**

## Contraste WCAG — calculé, pas supposé
Calcul du ratio de contraste relatif (formule WCAG 2.1, luminance relative sRGB) :

| Paire | Ratio | Seuil AA (texte normal) | Seuil AAA |
|---|---|---|---|
| Texte sidebar `#e0e0e0` sur fond `#1a1a1a` | **13.18:1** | 4.5:1 ✅ | 7:1 ✅ |
| Texte actif `#3ddc84` sur fond actif `#0f3d24` | **6.87:1** | 4.5:1 ✅ | 7:1 ❌ (proche, AA large) |

Les deux couples dépassent le seuil AA ; le texte sidebar principal dépasse même AAA. Aucun
ajustement nécessaire.

## Distinction `--accent-primary` (vert signature) vs `--success` (vert statut)
Risque identifié dans TASK-120 §Risques — vérifié en conditions réelles (valeurs calculées in-app
via `getComputedStyle`, cf. § Preuve ci-dessous) :
- `--accent-primary` = `#178a4c` (vert signature, boutons/liens actifs/monogramme)
- `--success` = `#2e7d32` (vert statut "ok", **inchangé**)

Deux teintes de vert distinctes (nuance et luminosité différentes — `#178a4c` plus saturé/foncé
que `#2e7d32`), visuellement séparables. Un audit visuel humain d'un écran réel cohabitant les deux
usages reste recommandé au premier vrai cas d'usage (aucun écran métier réel accessible cette nuit,
cf. § Limitation ci-dessous) — signalé, pas tranché ici.

## Preuve réelle — dev server actif, backend indisponible
Un serveur Vite dev de ce projet tournait déjà sur `http://localhost:5174` (process Node préexistant,
hot-reload actif — mes modifications `index.css`/`App.tsx`/`index.html` y sont visibles en direct
sans rebuild). Aucune instance de `Declaration.API` n'était en écoute (`http://localhost:5018` :
injoignable) : **le backend n'a pas été démarré cette nuit**, décision volontaire — le périmètre de
cette session interdit de toucher `Declaration.API`, et un autre chantier (TASK-115) est
explicitement en cours dessus en parallèle ; lancer/builder le backend concurremment aurait risqué
une interférence (verrous de build, état partagé) avec ce travail en cours. Ce choix de prudence est
documenté ici plutôt que contourné silencieusement.

Preuves obtenues sans backend, via Playwright (`chromium`, script ad-hoc supprimé après usage) :
- **Variables CSS réellement appliquées** (lues via `getComputedStyle(document.documentElement)`
  dans le navigateur réel, pas relues depuis le fichier source) :
  ```json
  {
    "--accent-primary": "#178a4c",
    "--accent-secondary": "#2fa868",
    "--accent-hover": "#0f6b3a",
    "--sidebar-bg": "#1a1a1a",
    "--sidebar-text": "#e0e0e0",
    "--sidebar-active-bg": "#0f3d24",
    "--sidebar-active-text": "#3ddc84",
    "--success": "#2e7d32"
  }
  ```
  → confirme que la palette est bien effective dans le navigateur, pas seulement dans le fichier
  source.
- **Balises favicon réellement présentes dans le DOM rendu** (4 balises `<link>`, `href` corrects).
- **Fichiers icône réellement servis** (`GET` direct sur le dev server) : `icon-dm.svg`,
  `icon-dm-32.png`, `icon-dm-192.png`, `icon-dm-512.png` → **200** chacun.
- `task120-01-licence-gate-sans-backend.png` : capture réelle de l'écran affiché. **Ce n'est PAS
  l'écran de connexion** — l'application applique un garde-fou licence *fail-closed* (TASK-117,
  `App.tsx` lignes 76-126) qui bloque l'affichage AVANT même le composant `Auth`/sidebar quand le
  backend est injoignable ou renvoie une licence invalide. Sans `Declaration.API` démarré, il est
  **structurellement impossible** d'atteindre l'écran de connexion, la sidebar ou un écran dense
  cette nuit — ce n'est pas un choix de scope de ma part mais un comportement produit existant
  (TASK-117), non contournable sans démarrer le backend (hors périmètre, cf. ci-dessus).

## ⚠️ Limitation assumée — captures manquantes
Les captures suivantes, demandées par TASK-120 §Livrables, **n'ont pas pu être produites cette
nuit** :
- (b) sidebar (fond sombre + icône DM + entrée active) — nécessite un login réussi → backend requis.
- (d) écran dense (Rapprochement/Affectations/Factures/liste) — nécessite un login réussi → backend
  requis.

Raison : garde-fou licence fail-closed (TASK-117) + décision volontaire de ne pas démarrer
`Declaration.API` cette nuit (hors périmètre strict de cette session + chantier TASK-115 concurrent
sur ce même backend, risque d'interférence). Compensé par : preuve DOM réelle des variables CSS
appliquées (§ ci-dessus, valeurs lues en live, pas supposées), grep de contrôle 0 résidu, calcul de
contraste chiffré, build vert. **Recommandation** : au réveil, le PO/l'architecte peut démarrer
`Declaration.API` localement, se connecter, et confirmer visuellement (b)/(d) en quelques minutes —
aucune régression de code n'est attendue (substitution de variables CSS pures, aucune logique
touchée), mais la confirmation visuelle humaine reste due avant clôture définitive.

## Build
```
npm run build   (tsc -b && vite build)
```
→ **Succès, 0 erreur.** (1 warning pré-existant `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`, non lié à
ce changement, présent avant TASK-120.)

## Critères de validation (task originale)
- [x] Nouvelle palette noir/vert visible et cohérente (sidebar, boutons, actifs, focus) — une seule
      source (variables CSS) : confirmé par lecture DOM réelle (`getComputedStyle`), grep 0 résidu.
- [x] Icône DM intégrée aux deux emplacements prévus (sidebar `App.tsx`, favicon `index.html`),
      mêmes fichiers source fournis (aucune variante recréée) : confirmé (DOM + `GET` 200 sur les 4
      fichiers). Rendu visuel en sidebar **non confirmé visuellement** (cf. Limitation).
- [x] Contraste sidebar texte/fond conforme : **13.18:1** (calculé, formule WCAG 2.1), très
      supérieur au seuil AA (4.5:1) et AAA (7:1).
- [ ] Aucune régression de layout/densité sur les grilles : **non vérifiable cette nuit** (backend
      indisponible, cf. Limitation) — code source inspecté, aucune modification de layout/structure
      apportée (seules des valeurs de couleur et une balise `<img>` remplaçant un `<span>`), risque
      de régression jugé nul par inspection mais non confirmé visuellement.
- [x] `--success` reste distinguable de `--accent-primary` : confirmé par valeurs hex distinctes
      (`#2e7d32` vs `#178a4c`), audit visuel humain sur écran réel recommandé au premier cas d'usage
      (cf. § dédiée ci-dessus).
- [x] Build front 0 erreur.

## Réserves / risques signalés (non tranchés, pour arbitrage PO/architecte)
1. **Captures (b)/(d) manquantes** — cf. § Limitation, action recommandée au réveil.
2. **`public/favicon.svg` orphelin** — ancien fichier non supprimé (hors périmètre explicite de la
   task, qui ne demande pas sa suppression) ; à trancher si nettoyage souhaité.
3. **Cohabitation vert signature / vert statut** — teintes distinctes par calcul, non auditées
   visuellement côte à côte sur un écran réel (cf. § dédiée).
4. **Dépendance TASK-121** : la task suivante a été démarrée cette même nuit sans attendre
   l'approbation de ce VERIFY (instruction PO explicite pour cette session) — risque de re-travail
   documenté séparément dans `VERIFY/TASK-121_verify.md` §Risque assumé.

## Statut
**Soumis pour revue** — implémentation nocturne en dérogation au rôle `CLAUDE.md`, ne s'auto-approuve
pas. Clôture officielle (déplacement vers `DONE_DETAIL/`, `TODO.md`/`DONE.md`/`CHANGELOG.md`) laissée
à l'architecte, conformément aux règles absolues de la session.
