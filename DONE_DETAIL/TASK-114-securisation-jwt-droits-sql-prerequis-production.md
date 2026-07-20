# TASK-114 — Sécurisation JWT + droits SQL dédiés (prérequis installation production)

## Contexte
Revue du guide `LANCEMENT_DEV.md` en vue d'une installation en environnement de production
(demande PO 17/07/2026). Deux manques identifiés en confrontant le doc au code réel
(`appsettings.json`, `Program.cs`, `AuthController.cs`, `DeclarationTVA.sql`) — **indépendants
de TASK-044** (qui couvre l'assemblage du dossier `deploy/` et la copie du worker net48) :

1. **Clé JWT de dev committée en clair et jamais surchargée en prod.**
   `Declaration.API/appsettings.json:15` contient
   `"SecretKey": "ThisIsASecretKeyForJwtAuthenticationThatMustBeLongEnough"` — valeur factice
   versionnée dans git, lue par `AuthController.cs:72` et `Program.cs:45` via
   `_configuration["JwtSettings:SecretKey"]`. `connections.json` (le seul fichier édité en prod
   selon `LANCEMENT_DEV.md`) ne couvre que `ConnectionStrings` + `WorkerConfig` — **pas**
   `JwtSettings`. Aucun `appsettings.Production.json` n'existe dans le repo (alors que
   `LANCEMENT_DEV.md` ligne 167 le référence comme s'il existait). Résultat en l'état : tout
   déploiement produit signe ses JWT avec une clé publique, connue de quiconque a accès au repo
   → un attaquant peut forger un token valide.
2. **Aucun script de droits SQL pour le compte applicatif.**
   `LANCEMENT_DEV.md` (table « Erreurs fréquentes », ligne 168) se contente de
   « Donner accès aux bases à l'utilisateur SQL » sans script ni périmètre précis. Les 3 connexions
   (`GrfConnection`, `SageConnection`, `PersistenceConnection`) pointent vers des bases existantes
   chez le client (`GR_EMA_DISTRIBUTION`, `BASE_SAGE`) — il n'existait aucun moyen reproductible de
   créer un compte SQL dédié à moindre privilège.
3. **`DeclarationTVA.sql` (racine) était lui-même obsolète.** Vérification code réel
   (`DeclarationRepository.cs`, `WorkflowEntities.cs`) vs script racine (17/07/2026) : 6 colonnes
   manquantes (`DM_ENTTVA.DT_Id` — TASK-094 ; `DM_LGTVA.EC_Id`/`MV_Id` — TASK-077 ;
   `DM_LGTVA.IncoherenceValidee`/`IncoherenceValideePar`/`IncoherenceValideeLe` — TASK-078),
   présentes dans des migrations séparées jamais reportées dans le script racine
   (`Declaration.Infrastructure/SQL/006_*.sql`, `007_*.sql`, `008_*.sql`). Table
   **entièrement absente** : `GRC_VENTILATION_SAGE_CACHE` (TASK-024/072/076, cache de ventilation
   Sage — code actif, `Declaration.Orchestration/VentilationSageCacheRepository.cs`). Trigger
   d'immuabilité **absent** du script racine : `TR_RT_AFFECTATION_Immuabilite` /
   `TR_RT_MOUVEMENT_Immuabilite` (TASK-064, `003_Verrou_DT_Id.sql`) — sans lui, une déclaration
   clôturée ne protège pas ses affectations contre modification/suppression côté ERP.

## Périmètre STRICT
- **Inclus** :
  1. **`DeclarationTVA.sql` (racine, script unique fusionné)** — livré : schéma complet à jour
     (4 tables + toutes migrations TASK-055/057/064/065/066/072/076/077/078/094/097), triggers
     d'immuabilité TASK-064, **et** login/droits SQL applicatif (`db_datareader` sur Grf/Sage +
     écriture ciblée sur les 4 tables de persistance + `UPDATE (DT_Id)` sur `RT_AFFECTATION` —
     jamais d'écriture sur le reste des tables ERP/Sage). Un seul fichier à lancer via `sqlcmd`.
  2. Mécanisme de secret JWT en prod : soit extension de `connections.json` avec une section
     `JwtSettings.SecretKey` (cohérent avec le principe « un seul fichier à éditer » du doc), soit
     `appsettings.Production.json` **hors dépôt** — trancher pour l'option la plus simple à
     opérer sans dupliquer la logique de `Program.cs`.
  3. Mise à jour de `LANCEMENT_DEV.md` : section « Créer les tables » et « Prérequis » à jour
     (le script crée aussi le cache Sage, les triggers et le compte SQL applicatif — plus besoin
     du flag `-d`, le script fixe son propre contexte via `USE`) ; correction de la référence à
     `appsettings.Production.json` (ligne 167) pour qu'elle corresponde à la solution retenue.
- **Exclu** :
  - Packaging du dossier `deploy/` et copie du worker net48 : **TASK-044**, ne pas dupliquer.
  - Rotation/renouvellement de la clé JWT en cours de vie (hors périmètre — dépôt initial suffit).
  - Chiffrement du fichier `connections.json` lui-même (déjà hors dépôt par convention existante).

## Objectif
```
Entrée  : DeclarationTVA.sql (tables), appsettings.json (secret factice versionné),
          LANCEMENT_DEV.md (procédure incomplète)
Étapes  : script de droits SQL moindre-privilège + mécanisme de secret JWT réel en prod
Sortie  : compte SQL applicatif créé avec droits strictement nécessaires ; clé JWT de prod
          jamais celle du repo ; doc de lancement à jour et sans référence à un fichier absent
```

## Étapes
1. ~~Écrire le script de droits SQL~~ **Fait** : fusionné dans `DeclarationTVA.sql` (racine) —
   schéma + migrations manquantes (§ Contexte point 3) + triggers TASK-064 + `CREATE LOGIN`/
   `CREATE USER`/`GRANT` par base, lecture large (`db_datareader`) sur Grf et Sage, écriture
   explicite limitée aux 4 tables de persistance + `UPDATE (DT_Id)` sur `RT_AFFECTATION`.
   Idempotent (vérifications `IF NOT EXISTS` partout).
2. **Trancher le mécanisme de secret JWT** avec le PO : extension `connections.json` (plus simple,
   cohérent avec l'existant) vs `appsettings.Production.json` (standard ASP.NET Core, mais
   nécessite de fixer `ASPNETCORE_ENVIRONMENT=Production` explicitement — à vérifier, absent de
   `sc.exe create` dans le doc actuel).
3. **Implémenter le mécanisme retenu** et vérifier qu'un token signé avec la clé de dev committée
   est bien rejeté une fois la vraie clé de prod en place.
4. **Mettre à jour `LANCEMENT_DEV.md`** : prérequis SQL (exécuter `DeclarationTVA.sql`, plus besoin
   de `-d`), prérequis secret JWT, correction de la ligne 167.

## Livrables
- `DeclarationTVA.sql` (racine repo, script unique) — **fait**.
- Mécanisme de secret JWT de prod implémenté + doc — **reste à faire**.
- `LANCEMENT_DEV.md` corrigé — **reste à faire**.
- `VERIFY/TASK-114_verify.md` : preuve réelle —
  - `DeclarationTVA.sql` exécuté sur une base de test vide crée bien le schéma complet (4 tables +
    triggers) et un compte qui **peut** lire Grf/Sage et écrire sur les 4 tables de persistance et
    `RT_AFFECTATION.DT_Id`, mais **ne peut pas** écrire sur une autre colonne/table ERP (test
    négatif explicite) ;
  - rejoué sur une base existante avec un schéma pré-TASK-077/078/094, le script rattrape bien les
    6 colonnes manquantes sans perte de données ;
  - un token JWT signé avec la clé factice du repo est **rejeté** par une instance configurée
    avec la vraie clé de prod.

## Critères de validation
- Aucun secret réel (mot de passe SQL, clé JWT) ne reste en clair dans un fichier versionné.
- Compte SQL applicatif à moindre privilège : écriture strictement bornée aux 4 tables de
  persistance + `RT_AFFECTATION.DT_Id`, jamais sur le reste des tables ERP/Sage.
- `LANCEMENT_DEV.md` ne référence plus de fichier inexistant.
- Aucune régression sur l'authentification existante (compte legacy `P_UTILISATEUR`, TASK-074).

## Risques / dépendances
- **Noms de bases variables selon le client** : `DeclarationTVA.sql` utilise
  `GR_EMA_DISTRIBUTION`/`BASE_SAGE` comme exemples (alignés sur `LANCEMENT_DEV.md`) — à adapter
  par le DBA/sysadmin selon l'environnement cible (recherche/remplacer documenté en tête de fichier).
- **`GrfConnection` et `PersistenceConnection` peuvent partager le même login** (les deux pointent
  vers `GR_EMA_DISTRIBUTION` dans l'exemple du doc) — le script suppose un seul compte pour les
  deux ; si le client exige des comptes distincts (lecture ERP vs écriture persistance), adapter.
- **Indépendant de TASK-044** : cette task ne touche ni au packaging `deploy/`, ni au worker —
  peut être livrée avant, après ou en parallèle.
