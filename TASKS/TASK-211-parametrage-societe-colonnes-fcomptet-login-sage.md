# TASK-211 — Écran de paramétrage société : login Sage + colonnes `F_COMPTET` (code activité, désignation document)

Status: 🆕 à faire
Priority: MEDIUM (socle bloquant pour TASK-212/TASK-213)
Module: Declaration.Infrastructure / Declaration.API / declaration-tva-web

> **Origine :** demande PO (09/08/2026) — créer 2 champs personnalisés sur la fiche fournisseur Sage
> (`F_COMPTET`) pour porter le code activité et la désignation de document, lus/modifiables par société,
> avec un écran de paramétrage exposant aussi le login/mot de passe Sage.

## Décisions de conception (tranchées avec l'architecte avant cadrage)

1. **Login/mot de passe Sage** : réutiliser les colonnes déjà existantes `P_SOCIETE.SO_ErpUserApp`/
   `SO_ErpPasswdApp` (déjà dynamiques par société depuis TASK-118, déjà utilisées par le worker OM) —
   **aucune nouvelle colonne**, uniquement un écran GRF pour les visualiser/modifier. Ne pas dupliquer
   ces identifiants dans une table GRF séparée.
2. **Noms des colonnes `F_COMPTET`** (code activité, désignation document) : contrainte PO explicite
   déjà actée (`DeclarationTVA.sql:526-528`, TASK-128) — **GRF ne modifie jamais le schéma de
   `P_SOCIETE`** (table partagée avec l'application principale). Ces 2 noms de colonnes sont donc
   stockés dans une **nouvelle table dédiée à GRF**, même patron que `DM_PARAM_DELAIPAIEMENT_SOCIETE`
   (`SO_Id` en référence logique, sans contrainte `FOREIGN KEY`).
3. **Validation des noms de colonnes** : même mécanisme que `IdentiteFiscaleFournisseurConfig.cs`
   (whitelist `^[A-Za-z0-9_]+$`, interpolation SQL uniquement après validation, jamais de saisie libre
   directement concaténée).

## Périmètre STRICT

- **Inclus** :
  1. Migration SQL idempotente (`DeclarationTVA.sql`, nouvelle section, même style que 1i) :
     `CREATE TABLE dbo.DM_PARAM_FCOMPTET_SOCIETE (SO_Id INT NOT NULL, ColonneCodeActivite NVARCHAR(128)
     NULL, ColonneDesignation NVARCHAR(128) NULL, UT_IdModif INT NULL, DateModif DATETIME2 NULL)` (nom
     de table/colonnes indicatif, à confirmer cohérent avec le style `DM_*` déjà en place).
  2. Repository + endpoint API `GET/PUT /api/societes/{soId}/parametrage` (ou équivalent) exposant : les
     2 noms de colonnes `F_COMPTET` (lecture/écriture sur la nouvelle table GRF) **et** le login/mot de
     passe Sage (lecture/écriture sur les colonnes `P_SOCIETE` existantes, `SO_ErpUserApp`/
     `SO_ErpPasswdApp` — écriture par mise à jour des colonnes existantes uniquement, jamais d'`ALTER`).
  3. Écran front (nouvel écran de paramétrage, un par société, réservé aux utilisateurs admin — cf.
     pattern déjà utilisé pour `UT_Admin` sur d'autres endpoints de configuration) : formulaire avec les
     3 champs (login Sage, mot de passe Sage, nom colonne code activité, nom colonne désignation).
  4. Mot de passe Sage : ne jamais le renvoyer en clair dans les réponses `GET` (masquer/vider le champ
     mot de passe en lecture, cohérent avec toute bonne pratique déjà en place ailleurs dans le projet
     pour des secrets — vérifier s'il existe déjà un pattern similaire avant d'improviser).
- **Exclu** :
  - Pas de câblage du code activité ni de la désignation dans les résolveurs/exports — objet de
    TASK-212 et TASK-213, qui dépendent de cette TASK.
  - Aucune modification de `P_SOCIETE` (ni ajout de colonne, ni changement de type).
  - Pas de création réelle des 2 colonnes personnalisées côté Sage `F_COMPTET` — c'est une action côté
    client/intégrateur Sage, hors périmètre GRF (GRF ne fait que lire le nom de colonne configuré et
    lira son contenu une fois qu'il existera côté Sage).

## Livrables

- Table SQL `DM_PARAM_FCOMPTET_SOCIETE` (ou nom validé), migration idempotente.
- Endpoint API paramétrage société (lecture/écriture).
- Écran front de paramétrage par société.

## Critères de validation

- Un admin peut configurer, pour une société donnée, le login/mot de passe Sage et les 2 noms de
  colonnes `F_COMPTET`, et les retrouver après rechargement de la page.
- Nom de colonne invalide (caractères hors whitelist) rejeté explicitement, pas d'injection SQL possible
  (même garde-fou que `IdentiteFiscaleFournisseurConfig`).
- Mot de passe Sage jamais renvoyé en clair par l'API en lecture.
- Build solution + tests existants non régressés.

## Files

- `DeclarationTVA.sql` (nouvelle section migration).
- [Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs](../Declaration.Selection/IdentiteFiscaleFournisseurConfig.cs) — patron de validation à répliquer.
- `Declaration.Infrastructure/Repositories/` (nouveau repository).
- `Declaration.API/Controllers/` (nouvel endpoint ou extension d'un contrôleur existant).
- `declaration-tva-web/src/` (nouvel écran, emplacement à déterminer — probablement dans le menu Paramétrage/Admin déjà existant).
