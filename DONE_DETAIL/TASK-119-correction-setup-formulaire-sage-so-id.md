# TASK-119 — Correction `Declaration.Setup` : retirer les champs Sage devenus obsolètes (impact TASK-118) + icône et nom de l'exe livré au client

## Contexte
TASK-118 (résolution dynamique de la connexion Sage par `SO_Id`, lue depuis `P_SOCIETE.SO_ErpDb`
+ `SO_ErpUserApp`/`SO_ErpPasswdApp`, `Server`/`User`/`Password` réutilisés depuis `GrfConnection`)
change le modèle de configuration Sage : ce n'est plus une valeur **unique et globale**, saisie
une fois pour toute l'installation, mais une valeur **résolue par société** à l'exécution.

Or `Declaration.Setup` (TASK-115, `IN_PROGRESS/`, VERIFY rejeté 18/07/2026 pour des réserves
**indépendantes** — 3 preuves GUI réelles manquantes, capture d'écran, décision Sage OM) a été
conçu et codé sur l'hypothèse **précédente** (une seule `SageConnection` + un seul `SageOM`,
saisis une fois au formulaire, écrits tels quels dans `connections.json`) :
- `Declaration.Setup/Models/SetupData.cs:14` — `SqlConnectionParts Sage` (Server/Database/User/
  Password Sage, une seule instance).
- `Declaration.Setup/Models/SetupData.cs:17-18` — `SageOmUser`/`SageOmPassword` (une seule paire
  d'identifiants applicatifs Sage OM pour toute l'installation).
- `Declaration.Setup/SetupForm.cs:19` (`_sage`, groupe « Sage (commerciale) »), `:22-23`
  (`_txtSageOmUser`/`_txtSageOmPassword`, groupe « Identifiants applicatifs Sage OM »,
  `:138-141`) — champs de saisie manuelle correspondants.
- `Declaration.Setup/Services/ConnectionsFileService.cs:45` (lecture `SageConnection`), `:51-53`
  (lecture `SageOM`), `:79` (écriture `SageConnection`), `:82-84` (écriture `SageOM`) — lit/écrit
  ces valeurs comme une config globale unique dans `connections.json`.

**Décision PO (18/07/2026) : suppression entière**, pas de fallback — la vraie source devient
`P_SOCIETE`, lue au runtime ; le setup n'a plus vocation à saisir une quelconque connexion/
identifiant Sage. Tranche du même coup TASK-118 §Périmètre point 5 (« fallback ou obsolète ») dans
le sens *obsolète*.

**Ce TASK ne remet pas en cause** les autres réserves déjà ouvertes sur TASK-115 (preuves GUI
réelles, capture d'écran, décision Sage OM `Unknown`) — il documente un impact **structurel
distinct**, découvert après coup, sur un sous-ensemble précis du formulaire.

**Demande PO complémentaire (18/07/2026)** — deux points de finition produit sur ce même
installeur, sans rapport avec l'impact TASK-118 mais regroupés ici (même fichier/projet) :
1. Une **icône** pour `Declaration.Setup` : `Declaration.Setup.csproj` a `<ApplicationIcon>`
   **vide** aujourd'hui (pas d'icône = icône Windows générique par défaut à l'exécution comme
   dans l'explorateur).
2. Le **nom de l'exe livré au client** doit être « Declaratif Maroc » — aujourd'hui
   `<AssemblyName>Declaration.Setup</AssemblyName>` (`Declaration.Setup.csproj:8`), donc l'exe
   généré est `Declaration.Setup.exe`, jamais renommé pour le client final.

## Périmètre STRICT
- **Inclus** :
  1. **Suppression complète** (tranchée PO, pas de fallback) : retirer `SqlConnectionParts Sage`
     et `SageOmUser`/`SageOmPassword` de `SetupData` (`Declaration.Setup/Models/SetupData.cs:14,
     17-18`).
  2. Retirer du formulaire (`Declaration.Setup/SetupForm.cs`) : le groupe « Sage (commerciale) »
     (`_sage`, ligne 19) et le groupe « Identifiants applicatifs Sage OM » (`_txtSageOmUser`/
     `_txtSageOmPassword`, lignes 22-23, 138-141) — ainsi que tout affichage/lecture associé
     (pré-remplissage en mode mise à jour, ligne 274/276-277 ; lecture au submit, ligne 289).
  3. Retirer de `ConnectionsFileService.cs` : lecture de `SageConnection`/`SageOM` (lignes 45,
     51-53) et écriture correspondante (lignes 79, 82-84). Vérifier si `SageConnection`/`SageOM`
     doivent être retirées de `connections.json`/`connections.json.exemple` eux-mêmes, ou
     seulement du formulaire (le fichier peut rester silencieusement vide/absent si plus rien ne
     le lit après TASK-118 — à vérifier une fois TASK-118 codée, pas de duplication de logique de
     lecture entre le setup et l'API).
  4. Mettre à jour `DOCS/DEPLOIEMENT.md` (section décrivant le formulaire, produite par TASK-115)
     en conséquence — les identifiants Sage OM par société (`SO_ErpUserApp`/`SO_ErpPasswdApp`)
     vivent désormais dans `P_SOCIETE`, gérés par l'application principale, pas saisis au setup
     GRF.
  5. **Icône de l'installeur** : produire un `.ico` multi-résolution (16/32/48/256, format
     Windows standard) et le référencer dans `<ApplicationIcon>` (`Declaration.Setup.csproj`).
     **Proposition de design** (cohérente avec l'identité déjà posée en TASK-083/093 — pas une
     nouvelle charte) : monogramme **« DM »** blanc sur fond **indigo profond `#2b4c7e`**
     (`--accent-primary`, `index.css`), carré à coins arrondis — reprend exactement le monogramme
     déjà utilisé dans la sidebar web (`App.tsx`, TASK-083) pour une cohérence visuelle
     web ↔ installeur ↔ (à terme) icône de service/barre des tâches. Alternative plus sobre encore
     si le PO préfère : un simple carré indigo uni sans lettre (juste la teinte signature comme
     repère visuel). Le fichier `.ico` lui-même (asset binaire) est à produire par un designer/dev
     à partir de cette spec — pas livrable en tant que texte dans ce document.
  6. **Renommage de l'exe livré au client** : `Declaration.Setup.csproj:8`
     `<AssemblyName>Declaration.Setup</AssemblyName>` → **`DeclaratifMaroc`** (recommandation :
     sans espace ni accent — cohérent avec l'id de service WinSW déjà choisi en TASK-115,
     `DeclaratifMaroc`, et sans risque de casse sur les scripts/chemins qui invoquent l'exe sans
     guillemets). Si le PO souhaite le littéral exact « Declaratif Maroc.exe » (avec espace), le
     signaler explicitement — techniquement possible sous Windows, mais moins pratique en ligne de
     commande/scripts qui devraient alors guillemeter systématiquement le chemin. Répercuter le
     nouveau nom partout où `Declaration.Setup.exe` est mentionné en dur (grep confirmé, 6
     fichiers hors doc de cette task) : `Deploy-All.ps1`,
     `Declaration.Setup/deploy/Publish-Setup.ps1`,
     `Declaration.Setup/Services/DeploymentCopier.cs`,
     `Declaration.Setup/Services/WinSwServiceManager.cs`, `DOCS/DEPLOIEMENT.md`,
     `LANCEMENT_DEV.md`.
- **Exclu** :
  - Toute autre réserve déjà ouverte sur `VERIFY/TASK-115_verify.md` (preuves GUI réelles,
    capture d'écran, décision Sage OM `Unknown`, `WorkerExePath`/version Sage) — hors périmètre
    de cette task, à traiter dans le cycle de révision propre de TASK-115.
  - Toute logique de résolution runtime elle-même (`IDbConnectionFactory`, `BuildOrchestrateur`,
    migration du cache) — c'est TASK-118, pas ce TASK.

## Objectif
```
Entrée  : Declaration.Setup formulaire avec champs Sage/SageOM globaux uniques (SetupData,
          SetupForm, ConnectionsFileService), écrits tels quels dans connections.json
Étapes  : retrait des champs Sage/SageOM (SetupData, SetupForm, ConnectionsFileService)
          → mise à jour DOCS/DEPLOIEMENT.md → icône .ico + renommage AssemblyName/exe
Sortie  : le formulaire de setup ne configure plus aucune connexion/identifiant Sage ; cohérent
          avec le modèle TASK-118 (résolution par SO_Id depuis P_SOCIETE, jamais au setup) ;
          l'exe livré au client porte l'icône et le nom du produit (« Déclaratif Maroc »), plus
          « Declaration.Setup.exe » générique
```

## Étapes
1. Retirer `SqlConnectionParts Sage`/`SageOmUser`/`SageOmPassword` de `SetupData`.
2. Retirer les groupes « Sage (commerciale) » et « Identifiants applicatifs Sage OM » de
   `SetupForm` (contrôles, pré-remplissage, lecture au submit).
3. Retirer les lectures/écritures `SageConnection`/`SageOM` de `ConnectionsFileService`.
4. Mettre à jour `DOCS/DEPLOIEMENT.md`.
5. Produire l'icône `.ico` (spec ci-dessus, monogramme « DM » sur indigo `#2b4c7e`) et la
   référencer dans `<ApplicationIcon>`.
6. Renommer `<AssemblyName>` en `DeclaratifMaroc` (ou confirmer avec le PO le littéral exact
   souhaité si différent) et répercuter dans les 6 fichiers identifiés (cf. Périmètre §6).
7. `VERIFY/TASK-119_verify.md` : preuve que le formulaire modifié reste cohérent (build 0 erreur),
   que l'exe généré porte le nouveau nom, et si possible capture/description du formulaire adapté
   + de l'icône visible (explorateur/barre des tâches) — même limite d'environnement GUI que
   TASK-115 à documenter si elle persiste.

## Livrables
- `SetupData.cs`/`SetupForm.cs`/`ConnectionsFileService.cs` adaptés.
- `DOCS/DEPLOIEMENT.md` à jour.
- `.ico` produit + `Declaration.Setup.csproj` (`ApplicationIcon` + `AssemblyName`) mis à jour.
- Les 6 fichiers référençant `Declaration.Setup.exe` en dur, mis à jour avec le nouveau nom.
- `VERIFY/TASK-119_verify.md`.

## Critères de validation
- Le formulaire de setup ne comporte plus aucun champ Sage/SageOM (ni saisie, ni pré-remplissage,
  ni écriture dans `connections.json`).
- Aucune régression sur les autres champs du formulaire (port, WinSW, JWT, ApLicence, version
  Sage — tous inchangés, hors périmètre de cette task).
- L'exe livré au client porte le nom du produit (pas « Declaration.Setup.exe ») et une icône
  visible (pas l'icône générique Windows par défaut).
- Build `Declaration.Setup` 0 erreur.

## Risques / dépendances
- **Bloqué tant que TASK-118 n'est pas implémentée** : cette task retire des champs devenus
  obsolètes du fait de TASK-118 — ne pas la livrer avant que la résolution dynamique par `SO_Id`
  soit réellement en place (sinon `connections.json` perdrait toute source de connexion Sage sans
  remplacement fonctionnel).
- **Risque de séquencement avec TASK-115** : si TASK-115 est approuvée et livrée en production
  **avant** TASK-118 + cette correction, des installations réelles auront un formulaire qui écrit
  des champs Sage/SageOM globaux devenus trompeurs dès qu'une 2ᵉ société avec une base Sage
  distincte apparaîtra. À signaler explicitement au PO au moment de décider l'ordre de livraison
  (TASK-118 → TASK-119 avant clôture définitive de TASK-115, ou acceptation explicite du
  correctif en fast-follow après une première mise en prod mono-société).
- **Dépend de TASK-118** (schéma de résolution Sage par `SO_Id`) — pas l'inverse.
- **Indépendant des réserves ouvertes de TASK-115** (preuves GUI réelles, Sage OM `Unknown`,
  capture d'écran) — celles-ci restent à la charge du PO indépendamment de cette task.
- **Nommage exe** : recommandation « sans espace » (`DeclaratifMaroc.exe`) documentée mais pas
  imposée — si le PO confirme vouloir le littéral avec espace, l'ajuster sans impact sur le reste
  de la task (changement d'un seul paramètre `AssemblyName`, propagé aux mêmes 6 fichiers).
- **Icône** : le fichier `.ico` est un asset binaire, à produire par un designer/dev à partir de
  la spec donnée ici — cette task documente le brief, ne livre pas l'asset elle-même en tant que
  texte.
