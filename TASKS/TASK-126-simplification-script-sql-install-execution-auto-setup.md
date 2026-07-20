# TASK-126 — Simplification `DeclarationTVA.sql` (retrait login dédié) + exécution automatique par le setup

## Contexte
Retour PO (19/07/2026) sur `DeclarationTVA.sql` (livré par TASK-114) : deux décisions actées en
session avec l'architecte.

1. **Retrait du login SQL dédié à moindre privilège** (`decl_tva_app`, section 3 du script).
   TASK-114 avait posé ce compte comme garde-fou explicite (« ne JAMAIS utiliser un compte
   admin/sa pour l'application »), avec un cloisonnement strict : `db_datareader` + écriture
   ciblée sur les 4 tables de persistance + `UPDATE (DT_Id)` sur `RT_AFFECTATION`. Le PO a été
   informé de ce recul de posture de sécurité et l'a **explicitement assumé** (arbitrage
   19/07/2026, alternative à moindre risque — automatiser la génération/écriture du mot de passe
   sans toucher au cloisonnement — proposée et écartée). Décision : l'application utilisera
   désormais le compte SQL déjà provisionné par le client/DBA à l'installation (droits larges,
   hors contrôle du produit).
2. **Exécution automatique du script par `Declaration.Setup`**, à la place de l'exécution
   manuelle `sqlcmd` par un DBA documentée dans `LANCEMENT_DEV.md`. Le script est déjà idempotent
   (`IF NOT EXISTS` partout, sections 1/2) — le risque « tables déjà existantes » est déjà couvert
   par construction ; reste à l'intégrer dans le flux du setup GUI (TASK-115).

**Compléments PO (même session, 19/07/2026)** :
3. **Le script doit aussi être livré tel quel dans le dossier d'installation du client** (pas
   seulement exécuté en interne par le setup) — pour audit/traçabilité et rejeu manuel éventuel par
   un DBA, au même titre que les autres artefacts du payload (`connections.json`, `WinSW\...`).
4. **Toute note interne de dev doit être retirée de la copie livrée** : références `TASK-XXX`,
   historique de découverte de bug, mentions d'environnements de dev (`.\sql2022`, `DISTRI_DEMO`...)
   n'ont rien à faire sous les yeux du client/DBA — seuls les commentaires expliquant une contrainte
   technique non-évidente (ex. pourquoi un `ALTER COLUMN` doit rester dans un batch `GO` séparé,
   pourquoi l'index du trigger n'est pas filtré) doivent être conservés, débarrassés de leur
   référence `TASK-XXX`.
5. **Le `USE <base>;` en tête de script doit être substitué dynamiquement** par le nom de base saisi
   par l'utilisateur au formulaire de setup, entre crochets `[ ]` (identifiant délimité SQL Server)
   pour supporter un nom de base contenant un caractère spécial.

## Périmètre STRICT
- **Inclus** :
  1. `DeclarationTVA.sql` : suppression complète de la section 3 (`3a` login niveau instance,
     `3b` droits base GRF, `3c` droits base Sage) et de la section 4 (requêtes de vérification
     liées à `decl_tva_app`). Ne restent que la section 1 (tables + migrations historiques) et la
     section 2 (triggers d'immuabilité TASK-064).
  2. En-tête du script à jour : retirer la ligne « `decl_tva_app` → nom de login SQL souhaité » et
     « `ChangeMe_MotDePasseFort!` → mot de passe réel » de la liste « À PERSONNALISER » ; ajouter
     une note explicite que le script s'exécute désormais avec le compte SQL fourni à
     l'installation (celui de `GrfConnection`/`PersistenceConnection`), qui doit disposer des
     droits DDL nécessaires (voir Risques).
  3. `Declaration.Setup` : composant d'exécution SQL intégré au flux d'installation/mise à jour
     (TASK-115) :
     - découpe le script en batches sur le séparateur `GO` (ADO.NET/`SqlCommand` ne l'interprète
       pas nativement) et les exécute séquentiellement sur `Grf.Database` ;
     - valide strictement le nom de base saisi dans le formulaire avant toute substitution dans
       une clause `USE`/objet qualifié (identifiant SQL, pas un paramètre de requête — refuser tout
       caractère hors `[A-Za-z0-9_]`) ;
     - échoue de façon explicite et bloquante en cas d'erreur (pas d'installation déclarée
       « réussie » avec un schéma incomplet ou partiellement appliqué) ;
     - s'exécute une seule fois par installation/mise à jour (au moment de la validation de la
       connexion GRF ou juste avant l'écriture de `connections.json`), jamais à chaque démarrage de
       l'API.
  4. `LANCEMENT_DEV.md` : retirer l'étape manuelle « exécuter `DeclarationTVA.sql` via `sqlcmd` » et
     toute référence à `decl_tva_app` ; documenter que le compte SQL saisi au formulaire de setup
     doit disposer des droits DDL nécessaires (voir Risques) pour que l'exécution automatique
     réussisse.
  5. **Nettoyage du script pour livraison client** : passe de relecture ligne à ligne de
     `DeclarationTVA.sql` retirant toute référence `TASK-XXX`, tout récit d'investigation/historique
     de bug, toute mention d'environnement de dev — **en conservant** les commentaires qui expriment
     une contrainte technique réelle (ex. contrainte de batch `GO` séparé avant/après un
     `ALTER TABLE ADD COLUMN` référencé ensuite, non-filtrage volontaire d'un index de trigger,
     sémantique métier d'une colonne). Décision : un **script unique** sert de source (pas de
     variante « dev » + variante « client » à maintenir en parallèle) — le script versionné dans le
     dépôt devient directement la version livrable ; l'historique/traçabilité par tâche continue de
     vivre dans `DONE_DETAIL/`, pas dans les commentaires SQL.
  6. **Substitution dynamique du `USE` en tête de script** : remplacer `USE GR_EMA_DISTRIBUTION;`
     par un jeton substitué par `Declaration.Setup` au moment de la génération du fichier livré,
     avec le nom de base saisi au formulaire, encadré par des crochets et échappé selon la règle
     T-SQL des identifiants délimités : tout `]` interne au nom doit être doublé (`]` → `]]`) —
     c'est la règle complète et suffisante pour un identifiant `[...]` (contrairement à un littéral
     `'...'`, il n'y a pas d'autre caractère à échapper à l'intérieur des crochets). Exemple :
     nom de base `Ma]Base` → `USE [Ma]]Base];`.
     ⚠️ **Recommandation architecte** : pour l'**exécution automatique** elle-même, ne pas dépendre
     de ce `USE` textuel — ouvrir directement la `SqlConnection` sur la base cible via
     `Initial Catalog=<nom>` dans la chaîne de connexion (déjà disponible, `Grf.Database`), ce qui
     évite tout risque de mauvaise substitution dans le texte exécuté. Le jeton `USE [...]` n'est
     à substituer que dans la **copie du fichier livrée en clair** dans le dossier d'installation
     (point 3), pour qu'un DBA qui le rejoue plus tard à la main (SSMS/`sqlcmd`) tombe directement
     sur la bonne base sans étape supplémentaire.
- **Exclu** :
  - Aucune modification du contenu fonctionnel des sections 1/2 (tables, migrations, triggers).
  - La section 3c (droits Sage) n'est pas adaptée au multi-Sage dynamique (TASK-118) — elle est
    purement supprimée avec le reste de la section 3, pas réécrite.
  - Aucune création automatique de compte SQL pour `SageConnection` : cette connexion reste résolue
    dynamiquement par `SO_Id` (TASK-118), hors périmètre de ce script.
  - Aucun mécanisme de rollback transactionnel du script en cas d'échec partiel (hors périmètre —
    voir réserve dans Risques).

## Objectif
```
Entrée  : DeclarationTVA.sql avec login dédié moindre privilège (copie manuelle de mot de passe),
          exécution manuelle sqlcmd par un DBA (LANCEMENT_DEV.md).
Étapes  : retrait de la section login (décision PO assumée) + exécuteur SQL intégré au setup
          (découpage batches GO, validation du nom de base, échec explicite et bloquant).
Sortie  : DeclarationTVA.sql réduit aux tables/migrations/triggers, nettoyé de toute note interne de
          dev ; Declaration.Setup exécute ce script automatiquement à l'installation/mise à jour
          (connexion directe sur la base cible, sans dépendre du `USE`), avec le compte SQL fourni
          par l'installateur ; une copie du script — `USE [nom_base]` substitué (crochets échappés)
          — est déposée dans le dossier d'installation du client pour audit/rejeu manuel.
```

## Étapes
1. Retirer les sections 3 et 4 de `DeclarationTVA.sql` ; mettre à jour l'en-tête (liste
   « À PERSONNALISER » et note sur les connexions).
2. Relire l'intégralité du script et retirer toute note interne de dev (références `TASK-XXX`,
   historique/récit de bug, environnements de dev) en conservant les commentaires de contrainte
   technique réelle (cf. Périmètre point 5).
3. Écrire le composant d'exécution SQL dans `Declaration.Setup` (découpage `GO`, connexion directe
   sur `Grf.Database` via la chaîne de connexion — sans dépendre du `USE` textuel, exécution
   séquentielle, remontée d'erreur bloquante).
4. Écrire la génération de la copie « livrée » du script : substitution du jeton `USE` par
   `USE [nom_base_échappé];` (doublement des `]` internes), écriture du fichier résultant dans le
   dossier d'installation (même mécanisme que `connections.json` — payload TASK-115/044).
5. Câbler ces deux composants dans le flux du wizard (TASK-115) — décider explicitement à quelle
   étape ils s'exécutent (proposition : juste après saisie/validation de la connexion GRF, avant
   l'étape récapitulative finale).
6. Mettre à jour `LANCEMENT_DEV.md` (retrait étape manuelle `sqlcmd`, retrait mentions
   `decl_tva_app`, ajout de la note sur les droits requis du compte SQL fourni à l'installation, et
   sur la présence du script dans le dossier d'installation).

## Livrables
- `DeclarationTVA.sql` réduit (sections 1/2 uniquement), nettoyé de toute note interne de dev,
  avec jeton de substitution pour le `USE`.
- Composant d'exécution SQL (connexion directe) + composant de génération de la copie livrée, dans
  `Declaration.Setup` + câblage dans le wizard.
- `LANCEMENT_DEV.md` à jour.
- `VERIFY/TASK-126_verify.md` : preuve réelle —
  - installation à blanc sur une base vide : schéma complet créé automatiquement (tables +
    triggers), sans étape `sqlcmd` manuelle ;
  - rejoué sur une base existante avec schéma pré-TASK-097/118 (tables partielles) : rattrapage
    sans perte de données, comme le script idempotent le garantit déjà ;
  - test négatif explicite : compte SQL fourni sans droits DDL suffisants → échec bloquant clair
    dans le formulaire, pas d'installation déclarée réussie ;
  - test avec un nom de base contenant un `]` : la copie livrée dans le dossier d'installation
    contient bien un `USE [...]` correctement échappé et rejouable tel quel dans SSMS/`sqlcmd` ;
  - relecture du script livré : confirmation qu'aucune référence `TASK-XXX`/note de dev ne
    subsiste.

## Critères de validation
- Plus aucune étape manuelle `sqlcmd`/copie de mot de passe dans le flux d'installation.
- `DeclarationTVA.sql` ne contient plus de section login/droits, ni de note interne de dev.
- Échec du script SQL = échec bloquant et explicite de l'installation, jamais silencieux.
- `LANCEMENT_DEV.md` ne référence plus `decl_tva_app` ni l'exécution manuelle du script.
- Le script effectivement appliqué est présent dans le dossier d'installation du client, avec le
  bon nom de base déjà substitué (`USE [...]` échappé), directement rejouable par un DBA.

## Risques / dépendances
- ⚠️ **Recul de sécurité assumé (décision PO 19/07/2026)** : l'application tourne désormais avec le
  compte SQL fourni à l'installation, potentiellement plus privilégié que le strict nécessaire
  (contredit le critère de validation initial de TASK-114 : « jamais admin/sa pour
  l'application »). Ce n'est plus un contrôle du produit — la limitation des droits, si souhaitée,
  redevient une responsabilité du client/DBA. À consigner clairement dans `LANCEMENT_DEV.md` pour
  que ce transfert de responsabilité soit visible, pas silencieux.
- Le compte utilisé doit disposer de droits DDL suffisants pour exécuter tout le script (y compris
  `CREATE TRIGGER`/`CREATE INDEX`/`ALTER TABLE` sur les tables ERP existantes `RT_AFFECTATION`/
  `RT_MOUVEMENT`, pas seulement les 4 tables `DM_*` propriété du module) — à vérifier explicitement
  avec le client que ceci est acceptable pour le compte qu'il fournira.
- Aucun rollback transactionnel si le script échoue à mi-parcours sur une base existante (DDL
  réparti sur plusieurs batches `GO`, non atomique) — l'échec doit être signalé clairement à
  l'utilisateur avec l'état exact atteint, jamais présenté comme un succès partiel.
- **Dépend de TASK-115** (wizard de setup) pour le point d'intégration dans le flux — TASK-115 est
  actuellement en `IN_PROGRESS` (refonte wizard 6 étapes) ; coordonner l'étape d'insertion de
  l'exécution SQL avec cette refonte plutôt que de la greffer sur l'ancien `TabControl`.
- Nom de base saisi au formulaire utilisé pour cibler l'exécution (`USE`/objets qualifiés) :
  entrée utilisateur, jamais interpolée sans validation stricte d'identifiant SQL (risque
  d'injection sinon). Pour la copie livrée en clair, le doublement de `]` suffit à couvrir
  l'échappement d'un identifiant délimité `[...]` — pas besoin de règle supplémentaire pour ce cas
  précis, mais l'exécution automatique elle-même ne doit **pas** reposer sur ce texte substitué
  (cf. recommandation Périmètre point 6 : connexion directe via `Initial Catalog`).
- **Nettoyage des notes internes = relecture manuelle, pas un filtre automatique** : un script de
  suppression de commentaires par motif (`grep -v "TASK-"` ou équivalent) risquerait de supprimer
  des commentaires qui expliquent une contrainte technique réelle simplement parce qu'ils citent
  aussi une tâche en passant — chaque commentaire doit être relu et reformulé au besoin, pas
  supprimé en bloc.
