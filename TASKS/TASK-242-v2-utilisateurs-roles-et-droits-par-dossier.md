# TASK-242 — V2 : utilisateurs, rôles et droits par dossier (multi-dossier)

Status: 🆕 à faire
Priority: HIGH (sans elle, la V2 ne peut ni s'authentifier ni isoler les dossiers d'un cabinet)
Risk: HIGH (sécurité, cloisonnement des données entre clients)
Module: Declaration.Compta.Persistence + Declaration.API + declaration-tva-web
Dépend de : TASK-225 ; front = TASK-231 + TASK-232

> **Origine :** PO 16/10/2026 — le système est **multi-société** : une installation gère plusieurs dossiers (une entreprise = une base compta Sage). La V1 s'appuie sur les utilisateurs de GRF (`UT_Admin`) ; en V2, GRF n'est plus la source, il faut un référentiel d'utilisateurs propre.

## Décisions de conception

1. **Utilisateurs stockés dans `DeclaratifMaroc`** (EF, migrations automatiques) : identifiant, nom, mot de passe haché avec sel (algorithme moderne standard de la plateforme, jamais de mot de passe en clair ni réversible), état actif/inactif, date de dernière connexion.
2. **Authentification** : jeton JWT (même principe que la V1, à relire dans `Declaration.API`) ; durée de vie limitée ; verrouillage temporaire après échecs répétés ; changement de mot de passe obligatoire à la première connexion.
3. **Rôles** : `Administrateur` (gère utilisateurs, dossiers, configuration), `Déclarant` (exporte, importe, déclare), `Lecteur` (consultation et export). Les droits sont attribués **par dossier** : un utilisateur peut être `Déclarant` sur A, `Lecteur` sur B, et sans accès sur C.
4. **Cloisonnement strict** : toute requête API est filtrée par les dossiers autorisés de l'utilisateur ; accéder à un dossier non autorisé renvoie une erreur explicite et journalisée (TASK-238). Aucun dossier n'est visible dans les listes si l'accès n'est pas accordé.
5. **Sélecteur de dossier** dans le shell (charte xGR, TASK-231) : le dossier actif est toujours affiché ; changer de dossier recharge tous les écrans.
6. **Actions sensibles réservées** : valider un écart, acquitter une alerte bloquante, confirmer une déclaration définitive = rôle `Déclarant` minimum ; configuration du dossier et gestion des utilisateurs = `Administrateur`. Chaque action est dans l'audit métier avec l'utilisateur.
7. **Premier démarrage** : création guidée du premier administrateur (pas de compte par défaut avec mot de passe connu).
8. **Hors périmètre** : fédération d'identité (SSO), authentification à deux facteurs, importation d'utilisateurs de la V1.
9. Documentation des rôles et droits dans le guide d'administration (TASK-240).

## Périmètre STRICT

- **Inclus** : tables et repository utilisateurs/rôles/droits par dossier, authentification et jetons, garde d'accès par dossier sur tous les endpoints V2, écran d'administration des utilisateurs, sélecteur de dossier, tests de sécurité.
- **Exclu** : SSO, 2FA, modification de l'authentification de la V1.

## Livrables

- Référentiel utilisateurs + authentification + droits par dossier + écran d'administration + sélecteur de dossier + tests.

## Critères de validation

- Un utilisateur sans droit sur le dossier B ne peut ni le lister, ni lire ses données, ni lancer d'export/import dessus (test automatisé sur chaque endpoint V2, réponse explicite et ligne de journal).
- Mots de passe : aucun en clair en base ni dans les logs (recherche jointe) ; verrouillage après N échecs vérifié.
- Un `Lecteur` ne peut pas valider un écart ni déclarer (testé) ; un `Déclarant` ne peut pas gérer les utilisateurs.
- Premier démarrage sans compte par défaut.
- Build + lint + build front 0 erreur ; **checklist UI** ; preuve par critère datée dans le VERIFY.

## Files

- `Declaration.API/` (relire l'authentification existante avant code), `Declaration.Compta.Persistence/`, `declaration-tva-web/src/Auth.tsx`, `src/` (lire `DOCS/UI_STANDARDS.md`).
