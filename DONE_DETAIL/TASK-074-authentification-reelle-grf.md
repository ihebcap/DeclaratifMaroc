# TASK-074 — Authentification réelle GRF (P_UTILISATEUR / P_SOCUTILISATEUR), remplacement du mock

> **Origine :** en creusant TASK-073 (garde de rôle pour la réouverture d'une déclaration), le PO
> signale (13/07/2026) que les utilisateurs du module Déclaration sont les utilisateurs GRF standard
> — `P_UTILISATEUR` (identité + `UT_Admin`) / `P_SOCUTILISATEUR` (droits par société) — et que le
> hashage du mot de passe est déjà résolu dans le projet `GRC_WEB`. Vérification architecte : le
> module GRF **n'utilise aucun de ces éléments aujourd'hui** — l'authentification est un mock complet.

## Constat (preuve code, aucune supposition)
1. **Login 100% mocké** — `Declaration.API/Controllers/AuthController.cs:23-57` (`POST /api/auth/login`) :
   accepte **n'importe quel login** dès lors que `Password == "admin"` (comparaison en dur, ligne 28),
   aucune requête vers `P_UTILISATEUR`. Le JWT émis contient un seul rôle statique
   `new Claim(ClaimTypes.Role, "User")` (ligne 42) — identique pour tout le monde, aucun `UT_Id`,
   aucun `UT_Admin`, aucune société.
2. **Aucune portée société par utilisateur** — `Declaration.API/Controllers/DeclarationsController.cs:45-50`
   (`GetAll`) accepte un `societeId` en query string sans aucune vérification que l'utilisateur courant
   a un droit sur cette société (`P_SOCUTILISATEUR` non consulté) : un utilisateur authentifié peut
   demander les déclarations de n'importe quelle société en changeant le paramètre.
3. **Le schéma cible existe et est déjà exploité ailleurs dans le SI** :
   - `P_UTILISATEUR` (`UT_Id`, `UT_Login`, `UT_Hash`, `UT_Salt`, `UT_Admin`) — `UT_Admin = 1` donne
     accès à toutes les sociétés (confirmé PO).
   - `P_SOCUTILISATEUR` (`SU_Id`, `UT_Id`, `SO_Id`) — liste les sociétés autorisées pour un utilisateur
     non-admin.
4. **Une implémentation réelle et fonctionnelle existe déjà dans `GRC_WEB`** —
   `d:\_vibe\GRC_WEB\GRC.API\Program.cs:138-154` : vérification du mot de passe via
   `Tresorerie.Infrastructure.PasswordHasher.Hash(req.Password, user.Salt)` comparé à `user.Hash`
   (DLL legacy, ligne 139-142), puis lecture de `UT_Admin` (ligne 151) et des droits associés
   (ligne 149, `P_UTILISATEURCAISSE` dans ce contexte trésorerie — l'équivalent GRF est
   `P_SOCUTILISATEUR`). C'est le patron à réutiliser, pas à réinventer.

## Objectif
Remplacer le mock `AuthController` par une authentification réelle contre `P_UTILISATEUR`, et faire
porter par le token/la session les informations nécessaires (`UT_Id`, `UT_Admin`, sociétés autorisées)
pour que **toute** la GRF (pas seulement Déclaration) puisse enfin vérifier un droit réel par société et
par rôle — dont le guard de réouverture demandé en TASK-073 dépend.

## Périmètre proposé
### A. Vérification réelle du mot de passe (bloquant)
Réutiliser `Tresorerie.Infrastructure.PasswordHasher` (ou équivalent partagé) contre
`UT_Hash`/`UT_Salt` de `P_UTILISATEUR`, à la place de la comparaison `"admin"` en dur.

### B. Claims réels dans le JWT (bloquant)
Le token doit porter `UT_Id`, `UT_Admin`, et la liste des `SO_Id` autorisés (vide/ignorée si
`UT_Admin = 1`, sinon dérivée de `P_SOCUTILISATEUR`).

### C. Garde société sur les endpoints existants (bloquant)
`GetAll` (`DeclarationsController.cs:45-50`) et tout endpoint filtrant par `societeId` doivent
vérifier que la société demandée fait partie des sociétés autorisées du token, sauf `UT_Admin = 1`.

### D. Fondation pour TASK-073 (non bloquant ici, mais dépendance déclarée)
Ce chantier ne décide pas qui a le droit de rouvrir une déclaration — il fournit seulement les
primitives (`UT_Admin`, société autorisée) sur lesquelles TASK-073 pourra s'appuyer une fois la
décision produit prise.

## Garde-fous
1. **Ne pas dupliquer le hashage** — si `PasswordHasher`/la DLL legacy est partageable entre
   `GRC_WEB` et `GRF`, la référencer plutôt que la réécrire.
2. **Pas de migration de schéma** — `P_UTILISATEUR`/`P_SOCUTILISATEUR` existent déjà et sont gérés
   ailleurs dans le SI ; ce chantier consomme, ne modifie pas leur structure.
3. **Compatibilité front** : le contrat `POST /api/auth/login` (forme de la requête/réponse) ne doit
   pas casser `declaration-tva-web` sans coordination — vérifier `AuthController`/consommateurs front
   avant de changer la forme de la réponse.

## Livrables de preuve (VERIFY)
1. Preuve réelle : connexion avec un login/mot de passe de `P_UTILISATEUR` (ex. table fournie par le
   PO) réussit ; mot de passe erroné refusé (test contre la vraie table, pas un mock).
2. Preuve réelle : un utilisateur non-admin restreint à une société ne peut pas obtenir les
   déclarations d'une autre société via `GetAll` (403/filtré).
3. Preuve réelle : un utilisateur `UT_Admin = 1` accède à toutes les sociétés.
4. Confirmation qu'aucune structure `P_UTILISATEUR`/`P_SOCUTILISATEUR` n'a été modifiée (diff limité à
   la couche auth/API de Déclaration).

## Dépendances / risques
- **Bloque partiellement** TASK-073 périmètre A (garde de rôle sur la réouverture) : sans identité
  réelle, aucune garde de rôle n'a de sens.
- **Risque** : dépendance à une DLL legacy (`Tresorerie.Infrastructure.PasswordHasher`) — vérifier
  qu'elle est accessible/référençable depuis `Declaration.API` sans coupler tout le module à
  `GRC_WEB`.
- **Aucun risque sur `GRC_WEB`** : lecture seule des tables partagées, aucune modification côté GRC.
