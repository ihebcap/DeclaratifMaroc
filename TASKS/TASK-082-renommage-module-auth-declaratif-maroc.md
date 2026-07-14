# TASK-082 — Renommer le nom de module sur la page d'authentification (« Déclaratif Maroc »)

## Contexte
Demande PO (13/07/2026) : sur la page d'authentification, le nom affiché est aujourd'hui **« Déclaration TVA »** (`Auth.tsx:79`, `<h1 className="auth-title">`). Or la **TVA n'est qu'un premier module** de la plateforme — d'autres modules sont prévus (cf. roadmap `TODO.md` §« Roadmap produit » : R1 RAS fournisseurs, R2 Télé-Déclaration, R3 Tableau de bord). Le nom de l'écran de connexion doit porter l'identité du **produit/plateforme**, pas celle d'un seul module, pour ne pas devoir le renommer à chaque nouveau module livré.

Nom retenu par le PO : **« Déclaratif Maroc »**.

## Périmètre STRICT
- **Inclus** : `declaration-tva-web/src/Auth.tsx` — le titre `<h1 className="auth-title">Déclaration TVA</h1>` (ligne 79) devient `Déclaratif Maroc`. Le sous-titre (`<p className="auth-subtitle">Accès sécurisé à l'espace de gestion</p>`, ligne 80) est conservé tel quel (déjà générique, ne nomme aucun module).
- **Inclus** : `declaration-tva-web/index.html` — balise `<title>` (ligne 7, actuellement `declaration-tva-web`, un nom de dossier technique jamais vu par l'utilisateur en usage normal) alignée sur `Déclaratif Maroc` pour cohérence de l'onglet navigateur.
- **Exclus / hors périmètre** (à traiter séparément si le PO le demande) :
  - Le libellé **« TVA »** dans l'en-tête de la sidebar post-connexion (`App.tsx:133`, `<span>TVA</span>`) — c'est le nom du **module actif**, distinct du nom de la page de connexion ; le confondre reviendrait à masquer quel module est ouvert une fois plusieurs modules disponibles. Pas touché ici.
  - Le nom technique du dossier/paquet `declaration-tva-web` (package.json, chemins de build) — cosmétique interne, aucun impact utilisateur, hors périmètre design.
  - Tout renommage de table, endpoint, ou entité back — aucun lien avec ce changement, purement front/texte.

## Cause racine
Le titre de la page d'authentification a été écrit au moment où « Déclaration TVA » était le seul module existant ; le nom du module et le nom du produit n'ont jamais été distingués dans le code.

## Objectif
```
Entrée : écran de connexion (non authentifié)
Traitement : remplacement du texte du titre, aucune logique touchée
Sortie : « Déclaratif Maroc » affiché en tête de la carte de connexion + onglet navigateur ;
         le nom du module (TVA) reste visible séparément une fois connecté (sidebar, inchangée)
```

## Étapes
1. `declaration-tva-web/src/Auth.tsx:79` : remplacer le texte `Déclaration TVA` par `Déclaratif Maroc`.
2. `declaration-tva-web/index.html:7` : remplacer `<title>declaration-tva-web</title>` par `<title>Déclaratif Maroc</title>`.
3. Vérifier qu'aucune autre occurrence de « Déclaration TVA » n'existe comme **nom de plateforme** ailleurs dans les écrans non authentifiés (`grep -n "Déclaration TVA" declaration-tva-web/src/Auth.tsx declaration-tva-web/index.html` = 0 après correctif).
4. Build front (`npm run build` / `tsc`) vert — changement de texte pur, aucune régression attendue.

## Livrables
- `Auth.tsx` avec le titre `Déclaratif Maroc`.
- `index.html` avec `<title>Déclaratif Maroc</title>`.
- `VERIFY/TASK-082_verify.md` : capture de l'écran de connexion montrant le nouveau titre + capture de l'onglet navigateur montrant le nouveau `<title>`, sortie build front sans erreur.

## Critères de validation
- La page d'authentification affiche « Déclaratif Maroc » (plus « Déclaration TVA »).
- L'onglet navigateur affiche « Déclaratif Maroc ».
- La sidebar post-connexion continue d'afficher « TVA » comme nom du module actif (non régressée, non touchée).
- Aucun changement de logique d'authentification (`handleSubmit`, appel `/auth/login`, sélection société) — texte seul.
- Build front 0 erreur.

## Risques / dépendances
- Risque quasi nul : changement de deux littéraux de texte, aucune logique.
- **Dépendance conceptuelle avec TASK-083** (thème visuel) : les deux touchent `Auth.tsx` — à livrer dans le même lot ou en séquence rapprochée pour éviter deux VERIFY sur le même fichier à quelques heures d'écart. Aucun conflit technique (zones différentes : texte du titre vs. styles).
- Point de vigilance pour une tâche future (non ouverte ici) : quand un 2ᵉ module réel sera livré, la sidebar (`App.tsx`) devra distinguer clairement « produit » (Déclaratif Maroc) et « module actif » (TVA / RAS / …) — à cadrer le moment venu, pas maintenant.
