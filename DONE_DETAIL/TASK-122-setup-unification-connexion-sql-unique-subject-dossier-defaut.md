# TASK-122 — Setup GUI (Déclaratif Maroc) : connexion SQL unique, subject ApLicence fixé, dossier d'installation par défaut, icône + charte noir/vert

## Contexte
TASK-115 (setup GUI WinForms) et TASK-117 (intégration vérification licence ApLicence) sont
**validées et livrées** (confirmation PO, 18/07/2026) — ce TASK ne rouvre pas leur périmètre, il
documente des **corrections post-livraison** demandées par le PO en observant le formulaire réel
(captures d'écran, session du 18/07/2026) :

1. **Connexion SQL unique** : le formulaire (onglet « Connexions SQL ») affiche aujourd'hui deux
   blocs distincts — « GRF (comptabilité) » et « Persistance (déclarations) » (`SetupData.Grf` /
   `SetupData.Persistence`, écrits dans `connections.json` sous les clés `GrfConnection` /
   `PersistenceConnection`). **Décision PO (18/07/2026)** : en réalité une seule base de données
   physique est utilisée pour les deux usages. L'installateur ne doit saisir qu'**une seule fois**
   Serveur/Base/Utilisateur/Mot de passe ; le setup écrit ensuite **la même valeur** dans les deux
   clés `GrfConnection` et `PersistenceConnection` de `connections.json`, en arrière-plan, sans que
   l'installateur ait à le savoir.
2. **Subject ApLicence fixé** : le champ « Subject (obligatoire) » de l'onglet « Sage & Licence
   ApLicence » ne doit plus être saisi/visible par l'installateur. Pour ce produit (Déclaratif
   Maroc), il est fixé à la constante **`TRESO_GRM_COM`**, écrite automatiquement dans
   `connections.json` sans champ formulaire.
3. **Dossier d'installation par défaut** : remplacer la valeur par défaut actuelle
   (`C:\DeclaratifMaroc`) par **`%ProgramFiles%\APBS\Declaratif Maroc`**, cohérent avec la
   convention Windows standard et le nommage produit (cf. TASK-093/TASK-115).
4. **Icône installeur alignée sur TASK-120** : `Declaration.Setup\Assets\AppIcon.ico` (monogramme
   « DM » sur indigo `#2b4c7e`, produit en TASK-119) est **remplacée** par l'icône « DM » livrée
   dans `TASKS/assets/task-120-icon-dm/` (`icon-dm.svg`, `icon-dm-512.png`, `icon-dm-192.png`,
   `icon-dm-32.png` — fond `#1a1a1a`, vert `#3ddc84`, mêmes fichiers source que la sidebar/favicon
   web, ne pas régénérer). **Point technique à traiter** : ces fichiers sont fournis en SVG/PNG, pas
   en `.ico` multi-résolution (16/32/48/256) requis par `<ApplicationIcon>` — reconversion nécessaire
   (à partir de `icon-dm-512.png`), pas de nouveau design.
5. **Charte graphique de l'installeur alignée (approximativement) sur TASK-120** (demande PO
   18/07/2026, « à peu près », pas une reproduction pixel-perfect) : le formulaire WinForms est
   actuellement en thème système par défaut (fond blanc, aucune couleur de marque). Réutiliser la
   palette déjà validée en TASK-120 (`--sidebar-bg` `#1a1a1a`, `--accent-primary` `#178a4c`,
   `--sidebar-active-text` `#3ddc84`) sur les éléments les plus visibles : en-tête/bandeau du
   formulaire (titre + icône DM) en fond sombre `#1a1a1a`, bouton « Installer » en vert signature
   `#178a4c`. **Le reste du formulaire (fond des onglets, champs de saisie) reste en thème système
   standard** — un restylage WinForms complet (tous les contrôles) est disproportionné par rapport à
   la demande et risque des régressions visuelles (lisibilité des champs, contraste) ; à confirmer
   avec le PO si un habillage plus poussé est réellement attendu avant d'aller plus loin.

## Périmètre STRICT
- **Inclus** :
  1. `Declaration.Setup\SetupForm.cs` — onglet « Connexions SQL » (`BuildConnectionsTab`) : un seul
     groupe de champs Serveur/Base/Utilisateur/Mot de passe affiché (remplace les deux groupes
     `_grf`/`_persistence`).
  2. `Declaration.Setup\Models\SetupData.cs` — au moment de la collecte (`BuildSetupData`/
     équivalent), la même valeur `SqlConnectionParts` saisie est assignée à **la fois** à `Grf` et
     `Persistence` (les deux propriétés du modèle sont conservées telles quelles pour ne rien casser
     côté `ConnectionsFileService`/`connections.json`, seule la **saisie** est fusionnée).
  3. **Mode mise à jour** : si un `connections.json` existant a des valeurs différentes pour
     `GrfConnection` et `PersistenceConnection` (installation antérieure à ce TASK), décider et
     documenter le comportement d'affichage/pré-remplissage (ex. n'afficher/pré-remplir que la
     valeur `GrfConnection`, avertir si elles diffèrent) — **à spécifier avant codage**, pas deviné.
  4. `SetupData.ApLicenceSubject` : supprimer le champ du formulaire (`SetupForm.cs`, onglet « Sage
     & Licence ApLicence ») et la validation associée (`MessageBox` « champ manquant »). La valeur
     est fixée en constante `TRESO_GRM_COM` au moment de la construction de `SetupData` (plus une
     saisie utilisateur).
  5. `SetupForm.cs` : valeur par défaut de `_txtInstallFolder` remplacée par
     `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\Declaratif Maroc"` (ou
     équivalent), au lieu de `C:\DeclaratifMaroc` codé en dur.
  6. `Declaration.Setup\Assets\AppIcon.ico` régénérée à partir de
     `TASKS/assets/task-120-icon-dm/icon-dm-512.png` (multi-résolution 16/32/48/256), remplace le
     fichier indigo existant, `.csproj` inchangé (`<ApplicationIcon>` pointe déjà sur ce chemin).
  7. `SetupForm.cs` : en-tête du formulaire (titre + icône) en fond `#1a1a1a`, bouton « Installer »
     en `#178a4c` — reste du formulaire inchangé (thème système).
  8. `VERIFY/TASK-122_verify.md` avec preuve réelle (voir Livrables).
- **Exclu** :
  - Toute modification du schéma `connections.json` lui-même (les clés `GrfConnection` /
    `PersistenceConnection` restent deux clés distinctes — seule la saisie utilisateur est
    fusionnée, pas le fichier de config ni les consommateurs `Declaration.Infrastructure`/
    `Declaration.API`).
  - `Declaration.API`/`Declaration.Infrastructure` (`DbConnectionFactory`, `IDeclarationRepository`,
    etc.) : aucun changement — ces couches continuent de lire deux clés séparées ; elles auront
    simplement la même valeur de connexion dans les deux, ce qui est transparent pour elles.
  - TASK-117 (vérification de licence côté `Declaration.API`) : ce TASK ne fait que fixer la valeur
    du subject côté setup ; l'usage réel du subject par `Declaration.API` reste le périmètre de
    TASK-117.
  - Migration d'une installation existante ayant déjà deux bases physiques différentes pour GRF et
    Persistence : hors périmètre (voir point ouvert en Risques).

## Objectif
```
Entrée  : formulaire setup avec 2 blocs connexion SQL distincts + champ subject obligatoire +
          dossier par défaut C:\DeclaratifMaroc
Étapes  : fusion des 2 blocs connexion en 1 seul (écriture dupliquée en arrière-plan dans
          connections.json) + subject fixé en constante TRESO_GRM_COM + dossier par défaut
          %ProgramFiles%\APBS\Declaratif Maroc
Sortie  : installateur ne voit qu'une seule saisie de connexion SQL et aucun champ subject ;
          connections.json reste inchangé dans sa structure (2 clés connexion + 1 clé subject,
          toutes correctement renseignées)
```

## Étapes
1. Fusionner l'UI des deux `ConnectionGroup` (`_grf`/`_persistence`) en un seul groupe dans
   `BuildConnectionsTab`.
2. Dans la collecte des données (`BuildSetupData` ou équivalent), dupliquer la valeur saisie vers
   `SetupData.Grf` et `SetupData.Persistence`.
3. Spécifier et implémenter le comportement de pré-remplissage en mode mise à jour si les deux
   clés existantes divergent (cf. Périmètre point 3).
4. Retirer le champ `_txtApLicenceSubject` et sa validation ; fixer `ApLicenceSubject = "TRESO_GRM_COM"`
   à la construction de `SetupData`.
5. Remplacer la valeur par défaut de `_txtInstallFolder`.
6. Régénérer `AppIcon.ico` depuis `icon-dm-512.png` (outil de conversion PNG→ICO multi-résolution).
7. Appliquer la couleur d'en-tête (`#1a1a1a`) et du bouton « Installer » (`#178a4c`).
8. Rejouer les builds Debug + Release.
9. Rédiger `VERIFY/TASK-122_verify.md`.

## Livrables
- `SetupForm.cs` modifié (un seul bloc connexion SQL, champ subject retiré, dossier par défaut mis
  à jour).
- `SetupData.cs` modifié si nécessaire (constante subject).
- `VERIFY/TASK-122_verify.md` : preuve réelle —
  - capture d'écran du formulaire : un seul bloc Serveur/Base/Utilisateur/Mot de passe sur l'onglet
    « Connexions SQL », plus de champ Subject sur l'onglet Sage & Licence ;
  - `connections.json` généré après une install à blanc : `GrfConnection` et `PersistenceConnection`
    identiques (même valeur saisie une fois), clé subject = `TRESO_GRM_COM` ;
  - dossier proposé par défaut = `%ProgramFiles%\APBS\Declaratif Maroc` ;
  - icône `.exe`/fenêtre = monogramme DM noir/vert (plus l'indigo TASK-119) ;
  - capture d'écran montrant l'en-tête `#1a1a1a` + bouton « Installer » `#178a4c` ;
  - build Debug + Release, 0 erreur/0 avertissement ;
  - test du mode mise à jour si un `connections.json` préexistant a des valeurs `GrfConnection`/
    `PersistenceConnection` différentes (comportement documenté à l'étape 3).

## Critères de validation
- Un installateur ne saisit la connexion SQL qu'une seule fois ; `connections.json` contient bien
  la même valeur dans les deux clés.
- Aucun champ « Subject » visible/saisi dans le formulaire ; `connections.json` contient
  `TRESO_GRM_COM` pour la clé subject.
- Dossier d'installation proposé par défaut = `%ProgramFiles%\APBS\Declaratif Maroc`.
- Aucune régression sur `Declaration.API`/`Declaration.Infrastructure` (lecture des clés
  `connections.json` inchangée).
- Aucun repli silencieux : si le formulaire ne peut pas déterminer un dossier par défaut valide
  (permissions, etc.), erreur explicite, pas un dossier au hasard.
- Icône `.ico` régénérée uniquement à partir des fichiers `TASKS/assets/task-120-icon-dm/` fournis
  par l'architecte — pas de nouveau design ad hoc.
- En-tête + bouton « Installer » aux couleurs TASK-120 ; aucun autre contrôle restylé sans validation
  PO explicite.

## Risques / dépendances
- **Dépend de TASK-115/TASK-117** (déjà livrées) — ce TASK modifie du code déjà en production ;
  vérifier avant déploiement chez un client que la mise à jour vers cette version ne casse pas une
  installation existante où `GrfConnection` ≠ `PersistenceConnection` (cf. Périmètre point 3, non
  tranché ici).
- **Non tranché — à spécifier avant codage** : comportement exact en mode mise à jour si les deux
  connexions existantes divergent (avertissement ? blocage ? pré-remplissage du premier seul ?).
- **Portée du "TRESO_GRM_COM" en dur** : ce TASK fixe cette valeur uniquement côté formulaire de
  setup (`Declaration.Setup`) ; l'utilisation réelle du subject dans la vérification de licence
  reste le périmètre de TASK-117 (déjà livré selon le PO — vérifier que cette même constante y est
  cohérente, sans quoi la licence échouera au démarrage).
- Si une volonté future de séparer à nouveau les deux bases apparaît (ex. montée en charge, isolation
  technique), ce TASK devra être revu — l'unification n'est pas présentée comme irréversible dans
  `connections.json` (les deux clés existent toujours), seulement dans l'UI de saisie.
- **Dépend des livrables TASK-120** (`TASKS/assets/task-120-icon-dm/`) — si TASK-120 change la
  palette/l'icône en cours de VERIFY (risque déjà noté dans TASK-120), ce TASK devra suivre pour
  rester cohérent (même source d'icône que la sidebar web/favicon).
- **Périmètre de la charte graphique volontairement limité** (en-tête + bouton principal) : un
  restylage plus profond du formulaire WinForms n'est pas déduit implicitement de « à peu près comme
  la nouvelle charte » — si le PO veut davantage, le signaler explicitement avant codage plutôt que
  laisser le développeur improviser l'étendue du restylage.
