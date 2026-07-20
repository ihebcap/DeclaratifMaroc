# VERIFY — TASK-115 : Setup GUI (WinForms) + service Windows via WinSW-x64 + port paramétrable

## Statut

Implémentation **code-complète** livrée. **Aucun run réel du GUI sur poste cible n'a été effectué
dans cette session** (environnement sans accès interactif Windows/UAC pour piloter une fenêtre
WinForms, et binaire `WinSW.exe` non téléchargé — pas d'accès réseau exercé). Les preuves
demandées par TASK-115 §Livrables (install à blanc, mise à jour détectée, port occupé) **ne sont
donc pas apportées** ici — cf. §Réserves bloquantes ci-dessous. Soumission pour revue architecte
avec ces réserves explicites, conformément à la règle CLAUDE.md « ne jamais improviser un
contexte manquant ».

## Ce qui a été livré

1. **Port paramétrable** — `Declaration.API/Program.cs` : lit `ServerConfig:Port` depuis la
   configuration (donc `connections.json`), défaut `5000`, applique
   `builder.WebHost.UseUrls($"http://+:{port}")` avant `builder.Build()`. Section `ServerConfig`
   ajoutée à `connections.json` (racine + `deploy\`).
2. **`ApLicence`** ajouté au schéma `connections.json` (`Subject`/`ServerAddress`/`ServerPort`,
   défauts `127.0.0.1`/`8003`, `Subject` sans défaut) — pré-requis pour TASK-117, sans effet
   fonctionnel tant que celle-ci n'est pas livrée (conforme à la note du PO).
3. **Vendorisation WinSW-x64** : `Declaration.Setup/WinSW/Get-WinSW.ps1` (script de récupération
   de la release officielle MIT, alternative retenue plutôt qu'un binaire committé, cf. réserve
   licence de TASK-115) + `Declaration.Setup/WinSW/DeclaratifMaroc.winsw.xml.template` (gabarit
   XML : id/nom de service, `startmode=Automatic`, `onfailure` restart, logs `roll-by-size`).
4. **Nouveau projet `Declaration.Setup`** (WinForms, `net10.0-windows`, ajouté à
   `DeclarationTVA.slnx`) :
   - `Models/` : `SqlConnectionParts` (parse/reconstruction d'une chaîne de connexion SQL),
     `SageVersion` (v7/v9/v10-couvre-v11/v12, v8 exclue), `SetupData`.
   - `Services/InstallDetector` : **signal de détection retenu et documenté** — présence de
     `connections.json` dans le dossier cible (justification en commentaire de code : signal déjà
     porteur de sens fonctionnel, indépendant du nom de service Windows qui change justement avec
     ce TASK). Voir §Réserves — ce choix n'a pas été validé par un test réel.
   - `Services/ConnectionsFileService` : lecture/fusion/écriture de `connections.json` via
     `JsonObject` — les mots de passe (SQL/SageOM/JWT) ne sont jamais extraits en clair pour
     pré-remplissage ; en mise à jour, un champ secret laissé vide **préserve** la valeur
     existante (jamais écrasé par du vide).
   - `Services/PortAvailability` : test de bind TCP avant installation.
   - `Services/PrerequisiteChecker` : `.NET Framework 4.8` vérifié via le registre
     (`NDP\v4\Full\Release >= 528040`, valeur documentée Microsoft) ; **Sage OM volontairement
     laissé `Unknown`** — aucun ProgID/CLSID Sage OM connu/confirmé par le PO à ce jour, une
     détection devinée aurait été une improvisation de contexte manquant. Le formulaire bloque
     tant qu'une case de confirmation manuelle n'est pas cochée.
   - `Services/NetFrameworkInstaller` : téléchargement + installation silencieuse
     (`/q /norestart`) du redistribuable officiel Microsoft si absent, avec information explicite
     du redémarrage possible (pas d'install « automatique et invisible »).
   - `Services/WinSwServiceManager` : recopie WinSW sous le nom `DeclaratifMaroc.exe`/`.xml`
     (substitution du gabarit), orchestre `install`/`start`/`stop`/`uninstall`/`status`.
   - `Services/DeploymentCopier` : copie les binaires du dossier source (celui de
     `Declaration.Setup.exe`) vers le dossier cible, en excluant explicitement
     `connections.json`, `WinSW\` et `Declaration.Setup.*`.
   - `SetupForm` : formulaire unique (dossier cible → détection auto → connexions SQL (3×
     Server/Database/User/Password) → SageOM → version Sage → licence ApLicence → JWT (génération
     aléatoire) → port) + pied de fenêtre statique **« © {année} APBS Groupe — Tous droits
     réservés »** + validations (Subject obligatoire, JWT obligatoire à l'install, port libre,
     confirmation Sage OM si indétectable).
5. **Nommage produit** : service Windows installé = **`DeclaratifMaroc`** (jamais `DeclarationTVA`
   ni « TVA » seul), cohérent avec TASK-093. Migration d'une installation `DeclarationTVA`
   existante **non traitée** (signalée, hors périmètre — cf. TASK-115 §Risques).
6. **Packaging** : `Declaration.Setup/deploy/Publish-Setup.ps1` — publie `Declaration.Setup`
   self-contained dans `deploy\` + copie `WinSW.exe` vendorisé. **Ne construit pas** le reste de
   `deploy\` (API/front/workers) : ce périmètre reste TASK-044, qui **n'a livré aucun
   `publish.ps1`** à ce jour (vérifié : absent du dépôt, `deploy\` actuellement peuplé à la main
   via les commandes de `LANCEMENT_DEV.md`). Documenté explicitement plutôt que fusionné/deviné.
7. **Documentation** : `DOCS/DEPLOIEMENT.md` créé (n'existait pas — TASK-044 ne l'avait pas
   produit) ; `LANCEMENT_DEV.md` §B/C/D/E réécrites (suppression de la référence erronée
   `appsettings.json → "Urls"`, remplacement des étapes manuelles par `Declaration.Setup.exe`) et
   ligne « Port déjà utilisé » du tableau d'erreurs corrigée.

## Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur, 0 avertissement.
- `dotnet build Declaration.API/Declaration.API.csproj` → 0 erreur (1 avertissement CS0105
  pré-existant, using dupliqué non lié à ce changement).
- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur, avertissements pré-existants
  uniquement (nullabilité/xUnit1012, non liés à ce TASK).

## Réserves bloquantes (à trancher/rejouer avant approbation)

1. **Aucune preuve réelle d'install/mise à jour/port occupé** (exigée par TASK-115 §Livrables) —
   le GUI n'a pas été exécuté en conditions réelles dans cette session (pas d'environnement
   interactif pour piloter une fenêtre WinForms élevée en administrateur). À rejouer sur un poste
   Windows réel avant clôture.
2. **`WinSW.exe` non téléchargé** — `Get-WinSW.ps1` n'a pas été exécuté (pas d'accès réseau
   exercé) ; `WinSwServiceManager.PrepareServiceFiles` lève une exception explicite si absent,
   comportement non vérifié à l'exécution.
3. **Détection Sage OM `Unknown` par construction** — nécessite soit un ProgID/CLSID confirmé par
   le PO, soit acceptation définitive du mode « confirmation manuelle » comme solution long terme.
4. **Dépendance TASK-044 non résolue** : `deploy\` reste assemblé à la main ; `Publish-Setup.ps1`
   n'a pas été exécuté de bout en bout contre un `deploy\` réel (le dossier `deploy\` présent dans
   le dépôt n'a pas été régénéré/testé avec ce script).
5. **Dépendance TASK-101/TASK-117** : la sélection de version Sage écrit un `WorkerExePath`
   cohérent avec les dossiers `deploy\workers\v7|v9|v10|v12\` existants (vérifié par lecture des
   noms de fichiers réels) ; les champs ApLicence restent sans effet tant que TASK-117 n'est pas
   intégrée (attendu, conforme au TASK).
6. **Formulaire WinForms non revu visuellement** (pas de capture d'écran — aucune session
   interactive Windows disponible pour l'exécuter).

## Recommandation

Ne pas approuver en l'état sans rejouer au minimum les 3 scénarios de preuve réelle du §Livrables
sur un poste Windows (install à blanc, relance = mise à jour détectée, port occupé → message
propre) et sans exécuter `Get-WinSW.ps1` + `Publish-Setup.ps1` de bout en bout.

## Suite au REJECT du 17/07/2026 — vérifications CLI complémentaires (18/07/2026)

Suite au rejet formel (raison : aucune des 3 preuves réelles apportée), le PO a choisi de rejouer
lui-même les 3 scénarios GUI sur poste réel (réserve n°1). Complément apporté ici : tout ce qui
est vérifiable **sans piloter la fenêtre WinForms** (réserves n°2 et n°4, partiellement).

1. **`Get-WinSW.ps1` exécuté réellement** (réserve n°2, levée) : téléchargement effectif de
   `WinSW-x64.exe` (release `v3.0.0-alpha.11`, 18 286 774 octets) depuis GitHub → renommé
   `Declaration.Setup/WinSW/WinSW.exe`. `WinSW.exe --version` répond `3.0.0+a6ba4168...` :
   binaire valide, pas une page d'erreur HTML déguisée.
2. **`Publish-Setup.ps1` exécuté de bout en bout contre un `deploy\` réel** (réserve n°4,
   partiellement levée) : `dotnet publish` self-contained win-x64 réussi, `Declaration.Setup.exe`
   + `WinSW\` (gabarit + script + `WinSW.exe`) ajoutés à `deploy\` existant sans erreur. Le reste
   de `deploy\` (API/front/workers) n'a pas été régénéré par ce script — hors périmètre, cf.
   réserve TASK-044 inchangée.
3. **Comportement si `WinSW.exe` absent, vérifié à l'exécution** (réserve n°2, complément) : test
   direct de `WinSwServiceManager.PrepareServiceFiles` (mini-projet jetable référençant
   `Declaration.Setup.csproj`, supprimé après usage) avec un dossier source sans `WinSW\WinSW.exe`
   → lève bien `FileNotFoundException` avec le message attendu, ne plante pas silencieusement.
4. **Cas nominal de `PrepareServiceFiles` vérifié** (avec le `WinSW.exe` réellement téléchargé) :
   `DeclaratifMaroc.exe` copié (même taille que la source), `DeclaratifMaroc.xml` généré, les deux
   placeholders `{{SERVICE_ID}}`/`{{SERVICE_NAME}}` bien remplacés par `DeclaratifMaroc` /
   `Déclaratif Maroc` (le XML généré ne contient plus aucun `{{...}}` en dehors du commentaire
   explicatif du gabarit, qui mentionne littéralement la syntaxe `{{...}}` à titre documentaire).
5. **Aucun service Windows réellement installé/démarré dans cette session** — pas d'action
   `winsw install`/`start` exécutée ici (aucun service `*Declaration*`/`DeclaratifMaroc` présent
   sur cette machine, vérifié via `Get-Service`) : décision explicite du PO de garder cette partie
   (avec le pilotage GUI) à sa charge sur poste réel.

### Réserves toujours ouvertes après ce complément

- Réserve n°1 (3 scénarios GUI réels install/mise à jour/port occupé) : **à la charge du PO**,
  pas rejouée ici — aucun outil de capture/pilotage GUI disponible dans cet environnement.
- Réserve n°3 (Sage OM `Unknown`) : inchangée, attente PO (ProgID/CLSID ou acceptation définitive
  du mode confirmation manuelle).
- Réserve n°4 : seule la part TASK-115 de `deploy\` est maintenant vérifiée de bout en bout ;
  TASK-044 (`publish.ps1` du reste de `deploy\`) reste non livrée.
- Réserve n°6 (capture d'écran / revue visuelle du formulaire) : inchangée, à la charge du PO.

**Ne pas ré-approuver sur la seule base de ce complément** : les réserves 1 et 6 (exécution réelle
du GUI) restent bloquantes tant que le PO ne les a pas rejouées lui-même.

## REJECT du 18/07/2026 (confirmation architecte)

Le complément CLI ci-dessus est reconnu (réserves 2 et 4-partielle levées avec preuve réelle),
mais **ne lève pas** le blocage : aucune des 3 preuves GUI (§Livrables), ni la capture d'écran
(réserve 6), ni la décision Sage OM (réserve 3) n'ont été apportées. Reste explicitement à la
charge du PO, sur poste Windows réel :

1. Rejouer install à blanc / mise à jour détectée / port occupé.
2. Capture d'écran du formulaire.
3. Trancher Sage OM (ProgID/CLSID confirmé, ou acceptation définitive du mode confirmation
   manuelle).

TASK-044 (reste de `deploy\`) est explicitement confirmée **hors périmètre** de TASK-115 et **non
bloquante** pour cette tâche. Tâche reste en `IN_PROGRESS/`, VERIFY non clos.

## REJECT du 18/07/2026 (nouvelle réserve — refonte UI wizard)

Le PO, après avoir lui-même piloté le formulaire `SetupForm` actuel (`TabControl` 3 onglets) sur
poste réel dans le cadre des réserves n°1/6 ci-dessus, signale ne pas apprécier cette
présentation et demande une refonte en **assistant multi-écrans (wizard)**, style Inno Setup
(référence explicite à un projet antérieur), un écran = une étape avec navigation
Suivant/Précédent. Périmètre tranché par question de cadrage architecte : **tout le formulaire**
(dossier d'installation, connexions SQL, Sage & Licence, service Windows/port, prérequis,
récapitulatif final), pas seulement les 3 groupes actuellement en onglets — cf. TASK-115 §Inclus
point 6bis (ajouté ce jour) pour le détail des 6 étapes retenues et les critères de validation mis
à jour.

**Nouvelle réserve bloquante n°4** (s'ajoute aux réserves n°1/3/6 toujours ouvertes, cf. REJECT
précédent) : `SetupForm.cs` doit être restructuré (TabControl → panneaux séquentiels + indicateur
d'étape + validation par étape), sans toucher aux services (`ConnectionsFileService`,
`WinSwServiceManager`, `PrerequisiteChecker`, etc.). Preuve attendue en VERIFY : capture d'écran de
chaque étape du wizard + build 0 erreur.

TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos. Réserves cumulées à lever avant nouvelle
soumission : n°1 (3 scénarios GUI réels), n°3 (décision Sage OM), n°4 (wizard, nouvelle), n°6
(capture d'écran — désormais une capture par étape).

## Complément du 18/07/2026 — refonte wizard livrée (réserve n°4)

**Avertissement sur cette entrée** : ce complément a été rédigé par l'agent qui a lui-même écrit
le code ci-dessous (dérogation explicite et ponctuelle à la règle CLAUDE.md « ne jamais modifier
le code source », actée par le PO le 18/07/2026 pour ce seul TASK). Il ne remplace pas une revue
indépendante — le PO doit faire valider ce VERIFY par un tiers (lui-même ou un autre reviewer)
avant clôture, l'auteur du code ne pouvant pas être aussi celui qui approuve.

### Ce qui a été livré

- `Declaration.Setup/SetupForm.cs` restructuré en assistant séquentiel (wizard) : le `TabControl`
  à 3 onglets et les panneaux « dossier »/« prérequis » toujours visibles sont remplacés par 6
  panneaux d'étape (`_stepPanels[0..5]`), un seul affiché à la fois, conformément à TASK-115
  §Inclus point 6bis :
  1. Dossier d'installation (+ détection install/mise à jour)
  2. Connexions SQL (GRF + Persistance)
  3. Sage & Licence ApLicence
  4. Service Windows (port)
  5. Prérequis (.NET 4.8 / Sage OM + confirmation)
  6. Récapitulatif final (relecture des valeurs saisies) + bouton Installer/Mettre à jour
- Navigation `Précédent`/`Suivant` avec indicateur d'étape (`Étape X / 6 — <titre>`) ; le bouton
  vert (style TASK-120) devient l'action de navigation `Suivant`, puis se change en
  `Installer`/`Mettre à jour` sur la dernière étape (remplace l'ancien `_btnInstallUpdate` fixe en
  pied de page).
- Validation par étape (`ValidateStepAsync`) avant de pouvoir avancer, au lieu d'une validation
  reportée en bloc au clic final : dossier obligatoire (étape 1), Serveur/Base/Utilisateur SQL
  obligatoires (étape 2), adresse licence ApLicence obligatoire (étape 3), port libre (étape 4),
  confirmation Sage OM + proposition d'installation .NET Framework 4.8 (étape 5). Le clic final
  (étape 6) ne fait plus que l'action d'installation/mise à jour elle-même.
- Bandeau de marque (`BuildHeaderPanel`) et pied de page (copyright TASK-122) restent fixes,
  visibles sur toutes les étapes — non modifiés.
- **Aucune logique métier modifiée** : `ConnectionsFileService`, `WinSwServiceManager`,
  `PrerequisiteChecker`, `PortAvailability`, `NetFrameworkInstaller`, `DeploymentCopier`,
  `InstallDetector` sont inchangés — seule la coquille UI (`SetupForm`) a été restructurée,
  conforme au périmètre tranché par l'architecte.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur, 0 avertissement.
- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur, 0 avertissement.
- Vérification par exécution réelle (et non simple lecture de code) : un harnais jetable
  (projet console temporaire, hors dépôt, référençant `Declaration.Setup.csproj`) instancie
  `SetupForm`, l'affiche, puis invoque par réflexion la méthode privée `GoToStep(0..5)` pour
  parcourir successivement les 6 étapes et capturer une image de la fenêtre à chaque étape.
  **Résultat inexploitable** : la session Windows de cet environnement est verrouillée
  (écran de verrouillage), les captures obtenues montrent le fond d'écran de verrouillage et non
  le formulaire — même limitation d'environnement que celle déjà documentée plus haut (pas de
  session interactive Windows/UAC pilotable). Le harnais et les captures ont été supprimés après
  ce constat (rien d'utile à conserver). La navigation entre étapes a néanmoins été exercée
  réellement via cet appel direct à `GoToStep` (pas seulement lue dans le code) : aucune exception
  levée sur les 6 transitions, ce qui exerce au minimum la construction des 6 panneaux et le
  recalcul de l'indicateur d'étape/bouton (`UpdateStepChrome`) sans crash.

### Réserves — état après ce complément

- **Réserve n°4 (wizard) : code livré et buildé, mais NON validée visuellement.** Aucune capture
  d'écran exploitable des 6 étapes n'a pu être produite (session Windows verrouillée dans cet
  environnement) — reste à la charge du PO sur poste réel, comme les réserves n°1 et n°6
  déjà ouvertes.
- Réserves n°1 (3 scénarios GUI réels), n°3 (décision Sage OM), n°6 (capture d'écran par étape) :
  **inchangées**, toujours à la charge du PO sur poste Windows réel.
- TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos. Ne pas approuver sur la seule base de ce
  complément : la refonte wizard doit encore être vue tourner (captures d'écran des 6 étapes) et
  revue par quelqu'un d'autre que l'auteur du code.

## Complément du 19/07/2026 — bug bloquant + 4 demandes UI corrigés (nouveau REJECT du PO)

**Même avertissement que le complément du 18/07/2026** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Suite à l'essai réel du PO (premier essai en conditions réelles depuis la refonte wizard) : un bug
bloquant identifié + 4 demandes UI/nommage. Corrections apportées :

### 1. Bug bloquant — gabarit WinSW absent du payload (Deploy-All.ps1:94)

- **Cause confirmée** : `robocopy $DeployPath $payloadStaging /MIR /XF connections.json
  "DeclaratifMaroc.*" ...` — le wildcard `"DeclaratifMaroc.*"` visait à exclure l'exe/pdb du setup
  lui-même du payload embarqué, mais robocopy `/XF` filtre par nom de fichier sans tenir compte du
  dossier : il excluait aussi `WinSW\DeclaratifMaroc.winsw.xml.template`, absent une fois extrait
  dans `%TEMP%\DeclaratifMaroc-Setup-<guid>\WinSW\`, d'où le `FileNotFoundException` de
  `WinSwServiceManager.PrepareServiceFiles` au clic Installer.
- **Correctif** : `Deploy-All.ps1:94` — remplacement du wildcard par les deux noms exacts
  `"DeclaratifMaroc.exe" "DeclaratifMaroc.pdb"`. Commentaire ajouté (§5 de la description du
  script) expliquant pourquoi un wildcard générique est proscrit ici.
- **Vérification réelle** (pas seulement lecture de code) : robocopy rejoué directement contre le
  `deploy\` existant sur ce poste vers un dossier de staging temporaire (supprimé après coup) —
  confirmé : `WinSW\DeclaratifMaroc.winsw.xml.template` présent dans le résultat,
  `DeclaratifMaroc.exe`/`.pdb`/`connections.json` toujours absents. Le comportement d'exclusion
  voulu est préservé, le bug de collision est levé.

### 2. Nom de service affiché

- `WinSwServiceManager.ServiceName` : `"Déclaratif Maroc"` → `"APBS Déclaratif Maroc"`.
- `SetupForm.cs` (étape Service Windows + récapitulatif) : retrait de la mention `(WinSW)` du
  libellé → `"Nom du service Windows : {ServiceName} ({ServiceId})"` /
  `"Service Windows : {ServiceName} ({ServiceId})"`.
- **Impact SCM vérifié par lecture du gabarit** (`WinSW/DeclaratifMaroc.winsw.xml.template`) :
  `ServiceId` (`"DeclaratifMaroc"`, inchangé) alimente `<id>`, l'identifiant SCM réel qui pilote la
  détection d'installation existante et la mise à jour. `ServiceName` n'alimente que `<name>`, le
  nom d'affichage dans la console des services Windows — champ cosmétique, sans effet sur
  l'identité du service ni sur une mise à jour depuis une version antérieure. Changement sans
  risque de ce point de vue.

### 3. Fenêtre / boutons de navigation sur une seule ligne

- Fenêtre réduite : `700×760` → `660×640`.
- Pied de page reconstruit (`BuildFooterPanel`) : l'ancien `TableLayoutPanel { AutoSize = true }`
  mélangeant une colonne `Percent(100)` et une colonne `AutoSize` (combinaison fragile, cause
  probable du débordement constaté) est remplacé par un panel `Dock.Fill`/`Height` fixe (44px) ;
  le groupe de navigation (`Annuler`/`Précédent`/`Suivant`) est ancré à droite
  (`Dock = DockStyle.Right`, `WrapContents = false`) et garde toujours sa largeur naturelle — il
  ne peut plus passer à la ligne, c'est le copyright (`Dock.Fill` + `AutoEllipsis`) qui rétrécit et
  se tronque en premier si la fenêtre est étroite.

### 4. Centrage des champs

- Nouvelle méthode `CenterHorizontally(Control content)` : héberge le contenu de chaque étape dans
  un panel qui recentre horizontalement (`content.Left` recalculé au `Resize`) tout en gardant le
  défilement (`AutoScroll`) en secours si le contenu dépasse. Appliquée aux 6 étapes
  (`BuildFolderStepPanel` … `BuildRecapStepPanel`), qui ne construisent plus chacune leur propre
  panel/scroll — largeur de contenu homogénéisée à 580px (au lieu de 620/640) pour tenir
  confortablement dans la fenêtre réduite.
- Effet esthétique général (densité/alignement plus proche du référentiel InnoSetup demandé) :
  traité par construction via la réduction de fenêtre + le centrage, pas de passe de style
  supplémentaire au-delà de ce périmètre précis.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur, 0 avertissement.
- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur, 0 avertissement.
- Robocopy rejoué en conditions réelles contre `deploy\` (cf. point 1) — résultat conforme.
- **Toujours pas de capture d'écran exploitable** : même limitation d'environnement que les deux
  compléments précédents (session Windows verrouillée). Le rendu visuel réel du footer/centrage/
  fenêtre réduite reste à valider par le PO sur poste réel — c'est précisément le point que la
  réserve n°6 couvre déjà.

### Réserves — état après ce complément

- Bug bloquant (payload WinSW) : **corrigé et vérifié en conditions réelles** (robocopy rejoué,
  pas seulement lu).
- Nom de service, fenêtre/boutons, centrage : **code livré et buildé, non validés visuellement**
  (réserve n°6 inchangée).
- Réserves n°1 (3 scénarios GUI réels), n°3 (décision Sage OM) : **inchangées**.
- TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos. Ne pas approuver sur la seule base de ce
  complément : reste à voir tourner (captures d'écran) et à faire revoir par quelqu'un d'autre que
  l'auteur du code — en particulier rejouer le scénario d'installation à blanc qui a révélé le bug
  bloquant, pour confirmer que le correctif Deploy-All.ps1 suffit de bout en bout (build complet du
  payload + extraction + `PrepareServiceFiles` réels, pas seulement le robocopy isolé vérifié ici).

## Complément du 19/07/2026 (bis) — bug fonctionnel 6/6 + encodage, révélés par le rejeu réel du PO

**Même avertissement que les deux compléments précédents** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Le PO a effectivement rejoué le scénario recommandé ci-dessus (installation/MAJ de bout en bout,
poste où le correctif payload avait déjà été appliqué) — cela a révélé un vrai défaut fonctionnel
distinct du bug payload, exactement comme prévu par la réserve. Nouveau REJECT reçu :

### 1. Bug bloquant — `RunInstallOrUpdate` déduisait Install() du mauvais signal

- **Symptôme** : étape 6/6, `Échec : WinSW start a échoué (code 1060)` — SCM : « service non
  installé ».
- **Cause confirmée** (`SetupForm.cs:650-678`, avant correctif) : le code décidait d'appeler
  `winSw.Install()` uniquement quand `_isUpdateMode` était faux — or `_isUpdateMode` est déterminé
  à l'étape 1/6 par la seule présence de `connections.json` dans le dossier cible, pas par l'état
  réel du service au SCM. Sur le poste de test du PO, un essai précédent (avant le correctif
  payload) avait écrit `connections.json` mais échoué avant d'atteindre `Install()` : le formulaire
  détectait donc "MISE À JOUR" et sautait `Install()`, faisant échouer `Start()` avec le code 1060
  (service non enregistré). `WinSwServiceManager.IsServiceRegistered()` existait déjà mais n'était
  jamais appelée à cet endroit.
- **Correctif** (`SetupForm.cs`, `RunInstallOrUpdate`) : la décision d'appeler `Install()` (et de
  faire `Stop()` avant recopie) repose maintenant sur `winSw.IsServiceRegistered()` — l'état réel du
  SCM — et non plus sur `_isUpdateMode`. `_isUpdateMode` continue de piloter uniquement le
  affichage/les valeurs par défaut du formulaire (`ConnectionsFileService.ApplyChanges`,
  `RefreshRecap`), pas la décision Install/Start.
- **Portée du changement** : `_isUpdateMode` (détection étape 1/6, affichage récap) n'est pas
  touché — seul le point de décision Install() dans `RunInstallOrUpdate` change de critère.
- **Vérification effectuée** : `dotnet build` (projet + solution complète) → 0 erreur. **Pas encore
  rejoué en conditions réelles** sur le poste du PO à ce stade (limitation d'environnement
  inchangée, session Windows verrouillée ici) — c'est précisément le scénario que le PO doit
  reproduire pour confirmer que `Install()` se déclenche désormais correctement dans son cas
  (`connections.json` présent, service jamais installé au SCM).

### 2. Bug secondaire — messages d'erreur WinSW/SCM corrompus (encodage)

- **Symptôme** : `sp,cifi,` au lieu de `spécifié` dans le message d'erreur remonté à l'utilisateur.
- **Cause confirmée** (`WinSwServiceManager.RunWinSw`, `WinSwServiceManager.cs:81` avant
  correctif) : `StandardOutput`/`StandardError` étaient lus sans encodage explicite sur le
  `ProcessStartInfo`. WinSW écrit sa sortie console dans la page de code OEM du poste (obtenue via
  `GetOEMCP()`, 850 en France), pas en UTF-8/1252 — sans le préciser, .NET décode ces octets avec le
  mauvais jeu de caractères, d'où la corruption des caractères accentués.
- **Correctif** : `StandardOutputEncoding`/`StandardErrorEncoding` du `ProcessStartInfo` fixés
  explicitement à la page de code OEM réelle du poste, interrogée dynamiquement (`GetOEMCP()` via
  P/Invoke `kernel32.dll`, pas codée en dur à 850) — portable si un poste client a une locale
  différente. Nécessite `System.Text.Encoding.CodePages` (ajouté en `PackageReference` dans
  `Declaration.Setup.csproj`) pour enregistrer `CodePagesEncodingProvider` : .NET (Core) n'embarque
  plus nativement les pages de code ANSI/OEM.
- **Vérification réelle effectuée** (pas seulement lecture de code) : petit programme .NET jetable
  (hors repo, `scratchpad/oemtest/`) reproduisant exactement la logique ajoutée —
  `GetOEMCP()` sur ce poste renvoie bien `850` ; un texte encodé en CP850 contenant
  « Le service spécifié n'existe pas en tant que service installé. » (le message SCM 1060 complet,
  reconstitué à partir du message tronqué du PO) est correctement redécodé caractère par caractère
  via `Encoding.GetEncoding(GetOEMCP())` après enregistrement du `CodePagesEncodingProvider`. Ce
  test confirme le mécanisme (P/Invoke + provider + décodage), pas le comportement de WinSW
  lui-même en conditions réelles (à confirmer par le PO au prochain essai, message d'erreur lisible
  attendu si un nouvel échec SCM survient).
- Avertissement `NU1510` à la restauration (« PackageReference ... ne sera pas supprimé ... »)
  observé sur les deux builds (projet + solution) — non bloquant (0 erreur), lié à la détection de
  pruning des packages NuGet du SDK, sans lien avec le fonctionnement du correctif.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur (2 avertissements NU1510).
- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur (20 avertissements, tous
  préexistants + les 2 NU1510 ci-dessus).
- Mécanisme d'encodage OEM vérifié par exécution réelle isolée (cf. point 2) — pas encore vérifié
  en conditions réelles via WinSW/SetupForm.
- Correctif Install()/IsServiceRegistered() **non rejoué en conditions réelles** dans ce complément
  (build seulement) — c'est le point le plus important à confirmer par le PO, puisque c'est
  exactement le scénario qui a révélé le bug.

### Réserves — état après ce complément

- Bug bloquant 6/6 (Install() manqué) : **corrigé par construction (relecture du code + build),
  PAS encore vérifié en conditions réelles** — à rejouer en priorité par le PO sur le poste où le
  bug s'est produit (connections.json déjà présent, service jamais installé).
- Bug encodage (messages WinSW/SCM) : **mécanisme vérifié isolément (test jetable), pas encore vu
  fonctionner via un vrai message WinSW/SCM en conditions réelles**.
- Bug payload (complément précédent), nom de service, fenêtre/boutons, centrage : inchangés depuis
  le complément précédent.
- Réserves n°1 (scénarios "installation à blanc" / "port occupé"), n°3 (décision Sage OM — à faire
  trancher explicitement par le PO comme choix définitif, pas comme comportement par défaut de
  fait) : **inchangées**, toujours à la charge du PO.
- Gouvernance : **toujours non résolue** — aucun VERIFY de ce dossier n'a encore été rédigé/revu par
  quelqu'un d'autre que l'auteur du code. TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.
- Prochaine étape recommandée pour le PO : rejouer très précisément le scénario qui a produit
  l'échec 1060 (même dossier cible, `connections.json` déjà présent, service jamais installé au
  SCM) pour confirmer que `Install()` se déclenche et que le service démarre ; profiter du même
  essai pour vérifier que les messages d'erreur (si un nouvel échec survient) sont désormais lisibles.

## Complément du 19/07/2026 (ter) — Declaration.API publié framework-dependent (option actée par le PO)

**Même avertissement que les compléments précédents** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Le PO a rejoué le scénario recommandé ci-dessus sur le poste de test (qui n'a que .NET 8 installé)
et a diagnostiqué lui-même la cause du non-démarrage du service : `Declaration.API` était publié
**framework-dependent** (`dotnet publish ... -c Release -o deploy`, sans `-r`/`--self-contained`)
alors que le service WinSW invoque `%BASE%\Declaration.API.exe` directement (cf.
`WinSW\DeclaratifMaroc.winsw.xml.template`), sans passer par la commande `dotnet` — sur un poste qui
n'a pas le runtime partagé .NET 10 (ici, .NET 8 seulement), cet apphost ne peut pas démarrer. Ce
défaut est distinct des deux bugs du complément précédent (Install() manqué, encodage) : ceux-ci
concernaient `Declaration.Setup`/WinSW, celui-ci concerne `Declaration.API` lui-même.

### 1. Correctif — publication self-contained de Declaration.API

- `Deploy-All.ps1`, étape 1/5 (`dotnet publish Declaration.API ...`) : ajout de
  `-r win-x64 --self-contained true`, à l'identique du choix déjà fait pour `Declaration.Setup`
  (étape 6/7 du même script).
- Commentaire ajouté expliquant le lien direct avec `<executable>%BASE%\Declaration.API.exe</executable>`
  du gabarit WinSW et le bug constaté sur le poste PO.

### 2. Vérification des autres points d'appel supposant un déploiement framework-dependent

Recherche effectuée sur tout le dépôt (`Declaration.API.exe`/`.dll`, invocations `dotnet`) :

- **`LANCEMENT_DEV.md:158`** — commande de publication manuelle (§A, procédure alternative au
  script) : avait la même omission (`dotnet publish ... -c Release -o deploy`, sans flags). Corrigée
  à l'identique (`-r win-x64 --self-contained true`) + commentaire, pour ne pas laisser une
  procédure documentée reproduire le même bug si un dev la suit à la main.
- **`deploy\web.config`** — généré automatiquement par le SDK ASP.NET Core à chaque publish (non
  committé, non modifié à la main) : avant correctif, `processPath="dotnet"
  arguments=".\Declaration.API.dll"` (traduit un hosting framework-dependent). **Vérifié
  empiriquement** : republication de `Declaration.API` en conditions self-contained dans un dossier
  jetable (`scratchpad/api-publish-test/`, hors repo) — le SDK régénère seul un `web.config` cohérent
  avec `processPath=".\Declaration.API.exe"` (plus de référence à `dotnet`/au `.dll`). Aucune
  intervention manuelle nécessaire sur ce fichier, il se corrige avec le flag de publication.
  Présence confirmée dans la sortie publiée de `hostfxr.dll`, `coreclr.dll`,
  `System.Private.CoreLib.dll` (runtime .NET embarqué) et de `Declaration.API.exe` — cohérent avec
  un déploiement réellement autonome, sans dépendance au runtime partagé du poste cible.
- **`DOCS\DEPLOIEMENT.md:13`** documentait déjà le self-contained comme « recommandé » avant même ce
  complément — c'est `Deploy-All.ps1`/`LANCEMENT_DEV.md` qui étaient en retard sur cette
  documentation, pas l'inverse. Aucune correction nécessaire sur ce fichier.
- **`test_api.ps1`** (`dotnet run --project ...`) et **`scratch\DeclaratifMaroc.xml`** (exemple
  ponctuel, dossier `scratch/` = diagnostic, hors pipeline actif) : ne dépendent pas de la
  publication de `deploy\` — hors périmètre, aucune modification.

### Vérifications effectuées

- Republication réelle de `Declaration.API` avec `-r win-x64 --self-contained true` vers un dossier
  jetable, hors repo → succès, 0 erreur (mêmes avertissements nullable préexistants, aucun nouveau).
- `web.config` régénéré inspecté directement (contenu confirmé ci-dessus, pas seulement supposé).
- `Deploy-All.ps1` revalidé syntaxiquement (`System.Management.Automation.Language.Parser`) après
  édition — aucune erreur de syntaxe PowerShell.
- **Non fait dans ce complément** : rejeu complet de `Deploy-All.ps1` de bout en bout (les 7 étapes
  réelles, avec build front/workers) et réinstallation sur le poste de test du PO — c'est exactement
  le point 3 demandé par le PO, qui reste à sa charge (cette session n'a pas accès à ce poste réel).

### Réserves — état après ce complément

- Bug "Declaration.API framework-dependent" : **corrigé (Deploy-All.ps1 + LANCEMENT_DEV.md) et
  vérifié par republication réelle isolée (web.config régénéré confirmé, runtime embarqué
  confirmé)** — **pas encore vérifié via un rejeu complet de `Deploy-All.ps1` ni une réinstallation
  réelle sur le poste PO**, qui est justement le test décisif (le seul qui peut confirmer le
  démarrage effectif du service sur un poste sans .NET 10 partagé).
- Bugs Install()/SCM et encodage (complément précédent) : statut inchangé, toujours à confirmer en
  conditions réelles par le PO.
- Bug payload, nom de service, fenêtre/boutons, centrage (compléments antérieurs) : inchangés.
- Réserve n°1 : **toujours ouverte**, explicitement maintenue par le PO tant que le scénario complet
  (rejeu `Deploy-All.ps1` + réinstallation + démarrage réel confirmé du service) n'a pas abouti avec
  succès de bout en bout sur son poste de test.
- Réserve n°3 (décision Sage OM) : inchangée.
- Gouvernance : **toujours non résolue** — aucun VERIFY de ce dossier n'a encore été rédigé/revu par
  quelqu'un d'autre que l'auteur du code. TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.
- Prochaine étape : PO à rejouer `Deploy-All.ps1` en entier sur son poste, réinstaller, et confirmer
  le démarrage réel du service — seul ce test lève la réserve n°1.

## Complément du 19/07/2026 (quater) — élimination de la duplication du subject ApLicence

**Même avertissement que les compléments précédents** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Demande PO : une seule source de vérité pour le subject ApLicence, jusqu'ici dupliqué en dur dans
deux projets (`Declaration.API/Program.cs:51` et `Declaration.Setup/Models/SetupData.cs:35`) —
risque de corriger un endroit et d'oublier l'autre.

### 1. Nouvelle source unique

- `Declaration.Application/Constants/LicenceConstants.cs` (nouveau fichier) :
  `public const string ApLicenceSubject = "/LIC/TRESO_GRF_COM";`.
- **Point d'attention signalé, pas décidé ici** : la valeur littérale fournie par le PO diffère de
  celle jusque-là en place dans le code (`"TRESO_GRM_COM"`, sans préfixe `/LIC/`, "GRM" et non
  "GRF") — cf. `TASK-122_verify.md`/`CHANGELOG.md` qui documentaient `"TRESO_GRM_COM"` comme valeur
  confirmée par le PO le 18/07/2026. Ce complément applique **exactement** la valeur donnée dans la
  demande de correction (`"/LIC/TRESO_GRF_COM"`), en tant que changement de valeur explicitement
  demandé par le PO — pas seulement une déduplication mécanique. À confirmer par le PO que ce
  changement de valeur (pas seulement l'emplacement) est bien intentionnel, avant tout essai réel
  contre le serveur de licence ApLicence (un mauvais subject ferait échouer la vérification de
  licence silencieusement différemment qu'avant).

### 2. Points d'appel mis à jour

- `Declaration.API/Program.cs` : suppression de la constante locale `ApLicenceSubject`, utilisation
  de `LicenceConstants.ApLicenceSubject` (commentaire de sécurité anti-contournement conservé et
  adapté).
- `Declaration.Setup/Declaration.Setup.csproj` : ajout de
  `<ProjectReference Include="..\Declaration.Application\Declaration.Application.csproj" />`.
- `Declaration.Setup/Models/SetupData.cs:35` : valeur par défaut `ApLicenceSubject` fixée à
  `LicenceConstants.ApLicenceSubject` au lieu du littéral local.

### 3. Effet de bord découvert et corrigé — collision de nom `Application`

L'ajout de la référence à `Declaration.Application` a cassé la compilation de `Declaration.Setup`
(WinForms) : `Application.Run(...)` (`Program.cs`) et `Application.ExecutablePath`
(`SetupForm.cs:169`) ne résolvaient plus vers `System.Windows.Forms.Application` (apporté par le
using global implicite de `UseWindowsForms`), mais vers l'espace de noms **`Declaration.Application`**
lui-même — collision de nom : depuis un fichier de namespace `Declaration.Setup`, l'identifiant non
qualifié `Application` résout d'abord vers le namespace frère `Declaration.Application` (recherche
depuis le namespace englobant `Declaration`), qui masque le using global vers la classe WinForms.
**Corrigé** en qualifiant pleinement les deux appels
(`System.Windows.Forms.Application.Run(...)` / `System.Windows.Forms.Application.ExecutablePath`) —
aucun autre appel non qualifié à `Application.*` trouvé ailleurs dans `Declaration.Setup` (recherche
exhaustive effectuée).

### Vérifications effectuées

- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur (échec initial CS0234 sur la
  collision `Application`, corrigé, puis re-build propre).
- `dotnet test DeclarationTVA.slnx` (suite complète) → 2 échecs, tous deux **préexistants et sans
  rapport avec ce changement** : `Declaration.Selection.Tests` (échec de connexion SQL réelle,
  environnement sans serveur SQL accessible) et `Declaration.Controle.Tests`
  (`GenererRapportVerification`, dépend d'une déclaration GRFN réelle absente de cet environnement)
  — tous deux des tests d'intégration nécessitant une base réelle, pas des tests unitaires sur le
  code touché ici. Aucun test ne référence la valeur du subject ApLicence.
- Recherche sur tout le dépôt de l'ancienne valeur (`TRESO_GRM_COM`) : ne subsiste que dans des
  archives historiques (`DONE_DETAIL/`, `CHANGELOG.md`, `DONE.md`) — non modifiées (archives, hors
  périmètre de la demande).

### Réserves — état après ce complément

- Déduplication (structure) : **faite et vérifiée par build + tests**.
- **Changement de valeur du subject** (`TRESO_GRM_COM` → `/LIC/TRESO_GRF_COM`) : appliqué tel que
  demandé, mais **non vérifié contre un serveur ApLicence réel** — cf. réserve TASK-117 déjà
  ouverte sur l'absence de vérification de licence en conditions réelles. À confirmer explicitement
  par le PO que ce changement de valeur est intentionnel (pas seulement un copier-coller de
  l'emplacement), avant tout essai réel.
- Empreinte de dépendances de `Declaration.Setup` augmentée (référence transitive à
  `Declaration.Core`/`Declaration.Selection`/`Declaration.Orchestration` + packages
  `Microsoft.Extensions.Configuration.Abstractions`/`System.Data.SqlClient` via
  `Declaration.Application`) pour une seule constante — signalé, pas corrigé : conforme à la
  demande explicite du PO, pas une dérive de périmètre de ma part.
- Tous les points ouverts des compléments précédents (Install()/SCM, encodage, payload,
  Declaration.API framework-dependent, réserves n°1/n°3, gouvernance/revue indépendante) restent
  **inchangés**. TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (quinquies) — port faussement signalé indisponible en mise à jour

**Même avertissement que les compléments précédents** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Bug bloquant sur le scénario "mise à jour" le plus courant : à l'étape 3/6, `ValidateStepAsync`
appelait `PortAvailability.IsFree(port)` sans distinguer le cas où le port occupé est celui du
service en cours de mise à jour lui-même (donc légitimement occupé) d'un port pris par un tiers.

### Correctif appliqué (`SetupForm.cs`)

- Nouveau champ `private int _originalPort;`.
- Après `_numPort.Value = data.Port;` (pré-remplissage du formulaire) : `_originalPort = data.Port;`
  — mémorise le port tel qu'il était avant toute saisie utilisateur.
- `ValidateStepAsync`, case 3 : `var portUnchanged = _isUpdateMode && port == _originalPort;` — le
  contrôle `PortAvailability.IsFree(port)` n'est exécuté que si `!portUnchanged`. Si l'utilisateur
  change le port en mode mise à jour, le contrôle habituel reste actif (nouveau port réellement
  soumis à `IsFree`) — pas de régression sur ce cas.
- Approche délibérément choisie par le PO plutôt qu'un arrêt anticipé du service à l'étape 3 pour
  tester le port : un `Stop()` prématuré resterait effectif même si l'utilisateur clique ensuite
  "Annuler", cassant une installation fonctionnelle sans raison. Comparer au port déjà connu est une
  vérification sans effet de bord.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur (avertissements
  préexistants inchangés).
- **Non vérifié en conditions réelles** dans ce complément (pas de service réellement démarré sur
  un port dans cet environnement) — à confirmer par le PO au prochain rejeu du scénario mise à jour.

### Réserves — état après ce complément

- Bug port/mise à jour : **corrigé par construction (relecture + build), pas encore vérifié en
  conditions réelles**.
- Tous les autres points ouverts (Install()/SCM, encodage, payload, Declaration.API
  framework-dependent, valeur du subject ApLicence, réserves n°1/n°3, gouvernance/revue
  indépendante) restent **inchangés**. TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (sexies) — verrou fichier après arrêt du service + copie non atomique

**Même avertissement que les compléments précédents** : rédigé par l'agent auteur du code,
dérogation ponctuelle actée par le PO, ne remplace pas une revue indépendante.

Deux corrections actées par le PO dans le même échange, la seconde étendant la première :

1. Après `winSw.Stop()`, le SCM peut mettre quelques instants à libérer les fichiers du service
   arrêté (handle process encore en cours de fermeture) : une copie ou régénération de fichier
   lancée immédiatement peut échouer par `IOException` (verrou).
2. Constat en essai réel sur le poste de test : une copie fichier par fichier n'est pas atomique.
   Un échec en cours de copie (verrou, disque plein, coupure...) laisse le dossier cible dans un
   état mixte ancien/nouveau incohérent — c'est ce qui a cassé le poste (apphost .NET 10 mêlé à des
   DLL runtime .NET 8 non remplacées).

### Correctif appliqué

**`SetupForm.cs`** — nouveau helper `RetryOnFileLock(Action action)` (retry avec backoff 500ms,
budget 15s, ne rattrape que `IOException`, laisse remonter l'exception telle quelle au-delà du
délai — pas de comportement silencieux, `OnFinishAsync` affiche déjà un message clair). Appliqué
dans `RunInstallOrUpdate` aux deux opérations qui suivent `Stop()` : l'appel à
`DeploymentCopier.CopyBinaries(...)` et l'appel à `winSw.PrepareServiceFiles(...)` (celui-ci
recopie `WinSW.exe` lui-même — même risque de verrou).

**`Services/DeploymentCopier.cs`** — réécrit pour rendre `CopyBinaries` atomique vis-à-vis du
dossier cible :
- Copie intégrale du payload vers un dossier de staging temporaire
  `{InstallFolder}\_update_staging` (nouvelle méthode privée `CopyToStaging`, reprenant la logique
  de parcours/exclusion précédente inchangée).
- Une fois tout le staging copié avec succès, bascule vers la cible par **déplacement**
  (`File.Move`, pas une nouvelle copie octet à octet) via une nouvelle méthode récursive
  `PromoteDirectory` : fusionne le contenu du staging dans le dossier cible (supprime puis déplace
  fichier par fichier) sans vider `InstallFolder` au préalable, pour préserver ce qui est hors
  périmètre de cette copie (`connections.json`, `DeclaratifMaroc.*`, `logs\`, fichiers WinSW
  générés).
- Le dossier de staging est supprimé dans un `finally` (succès comme échec) : jamais de résidu
  `_update_staging` visible dans une installation terminée.
- Si l'échec survient pendant le staging (avant `PromoteDirectory`), le dossier cible n'a reçu
  aucune modification de ses fichiers applicatifs — seul le sous-dossier de staging (nettoyé dans
  tous les cas) a été écrit.
- Combiné avec le retry-on-lock ci-dessus, qui reste nécessaire par ailleurs : `PromoteDirectory`
  elle-même peut ponctuellement rencontrer un fichier encore verrouillé (le `Stop()` n'est pas
  toujours instantané côté SCM), d'où l'utilité de conserver le retry autour de l'appel global à
  `CopyBinaries`, qui englobe désormais staging + promotion.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur (avertissements préexistants
  inchangés : `NU1510` CodePages, nullable-reference dans `Declaration.Selection`).
- **Vérification en conditions réelles du mécanisme de staging** (pas seulement relecture) : projet
  console jetable (`scratchpad/staging-test/testrunner`, hors dépôt) référençant une copie du
  fichier `DeploymentCopier.cs` tel que livré, avec un dossier source et un dossier cible simulant
  la structure réelle (fichiers applicatifs à remplacer, `connections.json`, `logs\` à préserver) :
  - **Scénario nominal** : `CopyBinaries` remplace bien le contenu applicatif (fichier existant
    écrasé, nouveau sous-dossier ajouté), préserve `connections.json` et `logs\app.log` intacts, et
    ne laisse aucun dossier `_update_staging` résiduel après succès.
  - **Scénario d'échec en cours de staging** (fichier source verrouillé en exclusif via
    `FileShare.None` avant l'appel) : l'`IOException` attendue est bien levée, **le dossier cible
    n'est pas modifié** (fichier applicatif resté à son ancienne valeur, `connections.json` intact),
    et le dossier de staging est bien supprimé malgré l'échec (pas de résidu).
- Le retry-on-lock (`RetryOnFileLock`) n'a pas été testé en conditions réelles de verrou SCM
  (nécessiterait un vrai service Windows démarré/arrêté) — vérifié par relecture uniquement.

### Réserves — état après ce complément

- Copie non atomique : **corrigée et vérifiée empiriquement** (scénario nominal + scénario
  d'échec, sur un dossier simulant la structure réelle) — pas encore vérifiée sur le poste de test
  réel qui avait produit l'incident (apphost/DLL runtime mêlés).
- Retry-on-lock après `Stop()` : **corrigé par construction (relecture + build), pas vérifié en
  conditions réelles** de verrou SCM.
- Tous les autres points ouverts des compléments précédents (Install()/SCM, encodage,
  Declaration.API framework-dependent, valeur du subject ApLicence, port/mise à jour, réserves
  n°1/n°3, gouvernance/revue indépendante) restent **inchangés**. TASK-115 reste en
  `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (septies) — RetryOnFileLock ne couvrait pas UnauthorizedAccessException

**Auto-évaluation, pas une revue indépendante.** Ce complément corrige un rejet précis formulé par
le Product Owner sur le complément précédent (sexies) ; comme pour tout le reste de ce fichier, je
n'approuve pas mon propre travail.

### Rejet du PO (constat technique)

Sur le poste de test, l'échec observé après `winSw.Stop()` était :
`UnauthorizedAccessException: Access to the path '...\ApLicence.Core.dll' is denied` — malgré une
exécution en administrateur. Le PO a établi que ce n'est pas un problème d'ACL/droits (le
lancement en administrateur ne change rien à un verrou de fichier transitoire) mais bien le même
phénomène de verrou après arrêt de service que celui déjà traité — sauf qu'ici .NET/Windows l'a
remonté en `UnauthorizedAccessException` plutôt qu'en `IOException`. Or `UnauthorizedAccessException`
dérive directement de `SystemException`, **pas** de `IOException` — ce sont deux branches distinctes
de la hiérarchie. Le filtre `catch (IOException) when (...)` du complément précédent ne rattrapait
donc pas ce cas : la première tentative échouait et l'exception remontait immédiatement, sans
retry, malgré le mécanisme censé couvrir exactement ce genre de situation.

Fichiers concernés (identiques à ceux cités par le PO) :
- `SetupForm.cs` — `RetryOnFileLock` (le filtre du `catch`).
- `DeploymentCopier.cs` — `PromoteDirectory` (`File.Delete`/`File.Move`), point d'origine probable
  de l'exception réelle observée (le fichier vient d'être libéré par l'arrêt du service).

### Correctif appliqué

**`SetupForm.cs`** — `RetryOnFileLock` élargi pour rattraper aussi `UnauthorizedAccessException` :

```csharp
catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && DateTime.UtcNow < deadline)
{
    Thread.Sleep(500);
}
```

Le comportement au-delà du délai de 15s est inchangé : la condition du `when` échoue, l'exception
(du type concret réellement levé) remonte telle quelle, sans transformation ni absorption.

### Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj` → 0 erreur (mêmes avertissements
  préexistants qu'avant, inchangés).
- **Vérification empirique avec une véritable `UnauthorizedAccessException`** (pas une exception
  fabriquée par un `throw` explicite) : projet console jetable (`scratchpad/retry-test`, hors
  dépôt) reprenant verbatim la logique de `RetryOnFileLock` (deadline paramétrable pour isoler le
  test B) :
  - **Test A** (verrou qui se résout pendant la fenêtre de retry) : fichier marqué `ReadOnly`, une
    tâche en arrière-plan retire l'attribut après 1,5s. `File.Delete` sur ce fichier lève une
    véritable `UnauthorizedAccessException` côté OS. Résultat observé : le fichier est supprimé
    avec succès, aucune exception ne remonte à l'appelant, durée écoulée ≈ 1,55s (cohérent avec le
    cycle retry 500ms + résolution à 1,5s) — confirme que le nouveau filtre attrape bien ce type
    d'exception et que le retry aboutit.
  - **Test B** (verrou persistant au-delà du délai) : même fichier `ReadOnly`, jamais libéré,
    deadline réduite à 2s pour le test. Résultat observé : `System.UnauthorizedAccessException`
    propagée telle quelle après ≈2,04s — confirme que le comportement « pas de silence au-delà du
    délai » est préservé pour ce type d'exception également.
- Ces deux tests utilisent une véritable contrainte du système de fichiers Windows (attribut
  `ReadOnly`), pas une simulation logique — la levée d'`UnauthorizedAccessException` est authentique
  et non fabriquée.

### Réserves — état après ce complément

- Filtre `RetryOnFileLock` trop étroit (`IOException` seul) : **corrigé et vérifié empiriquement**
  avec une véritable `UnauthorizedAccessException` provoquée par un attribut `ReadOnly` réel
  (scénario résolu pendant le retry, et scénario dépassant le délai).
- Reste non vérifié en conditions réelles : le scénario exact du poste de test (verrou provoqué par
  l'arrêt effectif d'un service Windows sur `ApLicence.Core.dll`, pas par un attribut `ReadOnly`
  simulé) — le mécanisme de retry couvre désormais le bon type d'exception, mais seul un rejeu réel
  sur le poste de test confirmera que ce verrou précis se résorbe bien dans la fenêtre de 15s.
- Copie non atomique (staging `DeploymentCopier`) : inchangé depuis le complément (sexies), toujours
  vérifié empiriquement sur structure simulée uniquement, pas sur le poste de test réel.
- Tous les autres points ouverts des compléments précédents (Install()/SCM, encodage,
  Declaration.API framework-dependent, valeur du subject ApLicence, port/mise à jour, réserves
  n°1/n°3, gouvernance/revue indépendante) restent **inchangés**. TASK-115 reste en
  `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (octies) — mini-repro isolé SqlConnection demandé par le PO

**Auto-évaluation, pas une revue indépendante.** Le PO a rejeté la poursuite des hypothèses sur un
échec de connexion SQL observé sur le poste de test (nature exacte du symptôme : voir échange
précédent, non reporté ici car décrit oralement/dans un message compacté, pas dans ce fichier) tant
qu'un test isolant les variables n'a pas été construit. Demande précise : une console app
`net10.0-windows`, publiée self-contained `win-x64`, référençant **uniquement**
`Microsoft.Data.SqlClient 7.0.2` (aucune autre dépendance du dépôt), qui fait juste
`new SqlConnection(connectionString)` + `Open()`.

### Ce qui a été construit

Projet jetable **hors dépôt** : `scratchpad/sqlclient-repro/` (n'apparaîtra jamais dans un commit).

- `sqlclient-repro.csproj` : `TargetFramework=net10.0-windows`, `RuntimeIdentifier=win-x64`,
  `SelfContained=true`, une seule `PackageReference` : `Microsoft.Data.SqlClient` `7.0.2`. Aucune
  référence à `System.Data.SqlClient`, à `Declaration.*`, ni à `Tresorerie.*`/`ApLicence.*`.
- `Program.cs` : lit la chaîne de connexion depuis `args[0]` (ou variable d'environnement
  `SQLCLIENT_REPRO_CONNSTRING` en repli) — **jamais codée en dur**, pour ne jamais faire transiter
  d'identifiants réels par un fichier, a fortiori un fichier de ce dépôt. Ouvre la connexion, affiche
  la version d'assembly `Microsoft.Data.SqlClient` chargée, puis en cas d'échec la chaîne complète
  des exceptions (type + message de chaque `InnerException`) et la stack trace — pour distinguer un
  échec réseau/auth normal (`SqlException`) d'un échec de chargement natif (`DllNotFoundException`,
  `TypeInitializationException`, `BadImageFormatException`...), ce qui est précisément la question
  posée par le PO.

### Vérifications effectuées ici (sur ce poste, pas le poste de test)

- `dotnet publish -c Release -r win-x64 --self-contained true` → succès, aucune erreur.
- Inspection du dossier `publish/` : présence de `Microsoft.Data.SqlClient.dll` +
  `Microsoft.Data.SqlClient.SNI.dll` (le composant natif) + ressources de traduction ; **recherche
  exhaustive de `System.Data.SqlClient*` dans `publish/` → aucun résultat**, confirmant l'isolation
  demandée (aucune cohabitation des deux piles SqlClient dans ce dossier).
- **Exécution réelle sur ce poste** (pas seulement une inspection statique) contre la chaîne de
  connexion `(localdb)` déjà présente en clair dans `Declaration.API/appsettings.json` (valeur
  factice, `GRFN_Dummy`, pas un secret) : le programme charge bien `Microsoft.Data.SqlClient
  7.0.0.0`, la pile réseau native (SNI) s'initialise sans erreur, et échoue seulement au niveau
  attendu — `SqlException` (error 52, "Impossible de localiser l'installation d'un Local Database
  Runtime") — parce que SQL Server LocalDB n'est pas installé sur ce poste. C'est un échec attendu
  et non ambigu : il prouve que le binaire self-contained et la pile SNI native fonctionnent
  correctement de bout en bout jusqu'à la tentative réseau, ce qui est la seule chose vérifiable ici.
- Package livrable : `scratchpad/sqlclient-repro/sqlclient-repro-publish.zip` (dossier `publish/`
  complet, self-contained, prêt à copier).

### Ce qui reste à faire — action PO, pas moi

Je n'ai **aucun accès au poste de test**. Le test demandé par le PO (faire tourner ce binaire *sur
le poste de test*, avec la *vraie* chaîne de connexion qui échoue en conditions réelles) ne peut être
exécuté que par le PO :

1. Copier `sqlclient-repro-publish.zip` sur le poste de test, extraire.
2. Lancer `sqlclient-repro.exe "<vraie chaîne de connexion>"` (ou définir
   `SQLCLIENT_REPRO_CONNSTRING` avant de lancer sans argument).
3. Rapporter la sortie complète (succès, ou le bloc `[0] ... [1] ...` + stack trace en cas d'échec).

Interprétation déjà actée avec le PO (rappelée ici, pas décidée par moi) :
- Si ce mini-repro **réussit** sur le poste de test → la cohabitation avec `System.Data.SqlClient
  4.9.1` (ou une autre dépendance du graphe de `Declaration.API`) est la cause probable → prochaine
  étape : isoler laquelle (candidats identifiés dans le dépôt : `Declaration.API.csproj` référence
  directement `System.Data.SqlClient 4.9.1` pour `Tresorerie.Dapper` ; `SageTaxReader.Core` aussi).
- Si ce mini-repro **échoue avec la même exception** → cause environnementale au poste (version
  Windows, GPO bloquant le chargement de DLL natives non signées, antivirus...), indépendante du
  code du dépôt → investigation à élargir hors du périmètre code.

### Réserves — état après ce complément

- Isolation du repro (structure, absence de `System.Data.SqlClient`, build/publish self-contained) :
  **faite et vérifiée sur ce poste**.
- **Le test décisif — exécution sur le poste de test réel avec la vraie chaîne de connexion — n'a
  pas été fait et ne peut pas l'être par moi.** Reste entièrement à la charge du PO.
- Tous les autres points ouverts des compléments précédents (Install()/SCM, encodage,
  Declaration.API framework-dependent, valeur du subject ApLicence, port/mise à jour, retry-on-lock
  vs poste réel, copie non atomique vs poste réel, réserves n°1/n°3, gouvernance/revue indépendante)
  restent **inchangés**. TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (nonies) — bissection System.Data.SqlClient : résultats mitigés, hypothèse non reproduite en isolation

**Auto-évaluation, pas une revue indépendante.** Suite au complément (octies), le PO a rejeté la
poursuite des hypothèses et demandé la bissection concrète : retirer `System.Data.SqlClient` de
`Declaration.API.csproj`, republier en self-contained, confirmer que le conflit SNI disparaît.

### Découverte n°1 — une deuxième référence non documentée, non utilisée

Retirer la seule ligne `Declaration.API.csproj:20` **ne suffit pas** : `Declaration.Application.csproj:11`
référence *aussi* `System.Data.SqlClient 4.9.1`, de façon totalement indépendante (ajoutée dans le
commit initial du projet, `fc259cd`, sans commentaire ni justification). Recherche exhaustive :
**aucun fichier `.cs` de `Declaration.Application` n'utilise ce namespace** — cette référence semble
être un reliquat de scaffolding, jamais consommé. Elle explique pourquoi la première tentative de
bissection (retrait uniquement dans `Declaration.API.csproj`) ne changeait rien : `Declaration.API`
référence `Declaration.Application`, qui réintroduit `System.Data.SqlClient` transitivement.
Confirmé par inspection de `project.assets.json` puis par republication : après retrait des **deux**
références, `dotnet publish -r win-x64 --self-contained true` ne contient plus que
`Microsoft.Data.SqlClient.SNI.dll` (recherche exhaustive de `sni.dll`/`System.Data.SqlClient*` dans
le dossier publié → aucun résultat). **Isolation confirmée possible côté build.**

Les deux `PackageReference` ont été **retirées puis immédiatement restaurées** dans les fichiers du
dépôt après cette vérification (`dotnet build` re-confirmé propre après restauration) — ceci reste
une expérimentation réversible, pas un changement décidé ; le dépôt est inchangé par rapport à avant
ce complément.

### Découverte n°2 — le retrait total casse le login, avant même d'atteindre le code suspecté

Test isolé (projet jetable hors dépôt, sans base réelle nécessaire) : `UtilisateurRepository.Get`
(le chemin legacy exact utilisé par `AuthController.Login`, ligne 46-48) sans `System.Data.SqlClient`
présent lève immédiatement `System.IO.FileNotFoundException: Could not load file or assembly
'System.Data.SqlClient, Version=0.0.0.0...'` — **avant toute tentative réseau**, donc indépendamment
de la validité de la chaîne de connexion. C'est un échec garanti, pas une hypothèse.

Conséquence logique importante : `AuthController.Login` appelle **d'abord** le chemin legacy
(`UtilisateurRepository.Get`, `System.Data.SqlClient`) puis **ensuite**, seulement en cas de succès,
`_connectionFactory.CreateGrfConnection()` (`Microsoft.Data.SqlClient`, ligne 57) — **dans la même
requête**. Un build sans `System.Data.SqlClient` casse donc l'étape 1 et **n'atteint jamais** l'étape
2 sur l'endpoint `/api/Auth/login`. Tester ce build via un login normal sur le poste de test ne
pourra donc pas confirmer ni infirmer l'hypothèse du conflit SNI — il faudrait taper un endpoint
authentifié qui appelle `CreateGrfConnection()`/`CreatePersistenceConnection()` **sans repasser par
le login legacy** (possible en réutilisant un JWT émis par le déploiement actuel non modifié, la clé
de signature `JwtSettings.SecretKey` étant une valeur de dev fixe dans `appsettings.json`).

### Découverte n°3 — la séquence exacte (legacy puis moderne) ne reproduit AUCUN conflit ici

Plutôt que de republier toute l'API (coûteux et biaisé par la découverte n°2), nouveau repro isolé
reproduisant la séquence **complète** de `AuthController.Login` dans un seul process console
(self-contained win-x64, les deux packages présents comme dans le code actuel) :
1. `UtilisateurRepository.Get(...)` (`System.Data.SqlClient`, chaîne bidon).
2. Immédiatement après, `new Microsoft.Data.SqlClient.SqlConnection(...).Open()` (chaîne bidon).

**Résultat observé sur ce poste** : les deux étapes échouent chacune avec une `SqlException` réseau
normale et attendue (serveur introuvable) — **aucune `DllNotFoundException`, aucune
`TypeInitializationException`, aucun `BadImageFormatException`, aucun crash du process**. Les deux
assemblies (`System.Data.SqlClient 4.6.1.6` et `Microsoft.Data.SqlClient 7.0.0.0`) cohabitent et
fonctionnent normalement dans le même process, dans le même ordre que le code réel. **Ceci contredit
l'hypothèse du conflit SNI natif, au moins sur cet environnement.**

### Ce que ça signifie — pas une conclusion, une nuance

- Je ne peux **ni confirmer ni exclure** la cause SNI : mon environnement diffère forcément du poste
  de test (version Windows, pilotes SQL Native Client/ODBC préexistants, stratégie de groupe,
  antivirus) — des facteurs que je ne peux pas reproduire ici et qui pourraient expliquer un résultat
  différent là-bas.
- Ce complément **affaiblit** l'hypothèse de cohabitation pure des packages comme cause suffisante
  (elle ne suffit pas à provoquer le crash dans un test isolé identique en séquence), sans l'éliminer
  complètement.
- Le retrait de `System.Data.SqlClient` (option du PO) reste **risqué et probablement inefficace** :
  casse le login de façon certaine (découverte n°2) et n'a pas démontré qu'il règle le problème
  (découverte n°3), en l'absence d'une confirmation sur le poste réel.
- La référence non utilisée dans `Declaration.Application.csproj` (découverte n°1) est un nettoyage
  légitime **indépendant** de cette investigation — signalé, pas fait, car hors du périmètre exact de
  cette bissection et pas forcément anodin sans confirmation du PO.

### Livrables pour le poste de test (le seul endroit où trancher définitivement)

- `scratch/sqlclient-repro-publish.zip` (complément octies) : `Microsoft.Data.SqlClient` seul.
- `scratch/sqlclient-bisection-publish.zip` (ce complément) : les deux stacks, séquence identique à
  `AuthController.Login`, self-contained win-x64. Lancer `sqlclient-bisection.exe` sans argument (la
  chaîne de connexion bidon est câblée en dur, volontairement — le signal cherché est le *type*
  d'exception à l'étape 2, pas une connexion réussie). Si ce repro plante différemment sur le poste
  de test que sur celui-ci (exception native au lieu d'une `SqlException` réseau, ou crash), la cause
  environnementale (vs. package) sera démontrée.
- `deploy-bisection-nosqlclient/` (dossier local, non zippé, ~121 Mo, non commité) : build complet de
  `Declaration.API` sans aucune des deux références `System.Data.SqlClient` — utilisable uniquement
  pour un test via un endpoint authentifié hors login (cf. découverte n°2), pas pour un test de login
  direct.

### Réserves — état après ce complément

- Cause du crash `Microsoft.Data.SqlClient` sur le poste de test : **toujours non identifiée avec
  certitude**. L'hypothèse SNI est affaiblie par ce complément, pas confirmée ni écartée.
- Référence `System.Data.SqlClient` inutilisée dans `Declaration.Application.csproj` : **signalée,
  pas supprimée** — à trancher par le PO (nettoyage légitime mais hors périmètre strict de cette
  demande).
- Tous les autres points ouverts des compléments précédents restent **inchangés**. TASK-115 reste en
  `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (decies) — élimination définitive de System.Data.SqlClient/Tresorerie.*

**Auto-évaluation, pas une revue indépendante.** Le PO a tranché après le complément (nonies) : plutôt
que de continuer à investiguer la cohabitation des deux piles SqlClient, éliminer la cause à la
racine. `AuthController.Login` était le **seul** point du dépôt à dépendre encore de
`Tresorerie.Dapper.UtilisateurRepository`/`ConnectionProvider` et `Tresorerie.Infrastructure.PasswordHasher`
(DLL precompilées legacy, figées sur `System.Data.SqlClient`) — le PO a retrouvé le code source réel
du hachage dans `D:\_vibe\apbs-gr_winform\src\Tresorerie.Infrastructure\PasswordHasher.cs` (HMACSHA512,
clé fixe, aucune dépendance externe) et le mapping exact de `P_UTILISATEUR`.

### Correctifs appliqués

- **`Declaration.Infrastructure/Security/PasswordHasher.cs`** (nouveau) : réimplémentation native de
  `Hash(password, salt)`, algorithme et clé identiques à l'original (nécessaire pour rester compatible
  avec les valeurs `UT_HASH`/`UT_SALT` déjà en base). `HashOldVersion` (clé différente) non porté,
  confirmé non utilisé par `AuthController`.
- **`AuthController.cs`** : `Login` n'instancie plus `UtilisateurRepository`/`ConnectionProvider` —
  requête Dapper directe via `_connectionFactory.CreateGrfConnection()` (déjà
  `Microsoft.Data.SqlClient`, déjà utilisé pour la requête `P_SOCUTILISATEUR` juste après) sur
  `P_UTILISATEUR`, mappée vers une nouvelle classe `UtilisateurRow` (remplace le modèle
  `Tresorerie.Dapper.Models.Utilisateur`). Les deux requêtes (utilisateur + sociétés autorisées)
  partagent désormais la même connexion — une couche de moins, comportement identique. `using
  Tresorerie.Dapper;`/`using Tresorerie.Dapper.Repositories;` retirés.
- **`Declaration.API.csproj`** : suppression de `PackageReference System.Data.SqlClient 4.9.1` et du
  bloc `<Reference>` complet vers `Tresorerie.Core/.Dapper/.Infrastructure`/`DapperExtensions`
  (commentaires TASK-074 associés retirés, devenus obsolètes).
  `Declaration.API/libs/Tresorerie/` supprimé physiquement du disque (dossier jamais suivi par git —
  `git rm --cached` a confirmé l'absence de tout suivi, donc aucun impact sur l'historique).
- **`Declaration.Application.csproj`** : suppression de la référence `System.Data.SqlClient 4.9.1`
  identifiée comme inutilisée dans le complément (nonies) — confirmée sans effet ici aussi (le
  nettoyage demandé par le PO avant l'interruption de la question précédente).

### Vérifications effectuées

- `dotnet build DeclarationTVA.slnx` (solution complète) → 0 erreur, avertissements inchangés
  (préexistants, sans rapport).
- `dotnet publish Declaration.API/Declaration.API.csproj -c Release -r win-x64 --self-contained true`
  → succès. Recherche exhaustive dans le dossier publié : **plus aucune trace** de
  `System.Data.SqlClient*`, de l'ancien `sni.dll`, ni d'aucune DLL `Tresorerie*`/`DapperExtensions*` —
  **seul reste `Microsoft.Data.SqlClient.SNI.dll`**. Un seul provider SQL dans tout `Declaration.API`,
  confirmé par inspection directe du dossier de publication, pas seulement par lecture du `.csproj`.
- Recherche exhaustive sur tout le dépôt (hors `scratch/decompiled/`, matériel de référence non
  compilé) : plus aucun fichier `.cs` ni `.csproj` ne référence `Tresorerie.*`,
  `UtilisateurRepository` ou `ConnectionProvider` (Tresorerie), en dehors des deux fichiers modifiés
  ici.
- Dossiers de publication temporaires créés pendant la bissection (`deploy-bisection-nosqlclient/`,
  `deploy-task115-final/`) supprimés après vérification (jamais suivis par git, purement locaux).

### Ce qui reste non vérifié

- **Vérification en conditions réelles sur le poste de test** : le crash `Microsoft.Data.SqlClient`
  disparaît-il réellement une fois `System.Data.SqlClient` totalement absent du publish ? C'est la
  seule vérification qui compte vraiment, et je ne peux pas la faire moi-même (pas d'accès au poste).
  Republier avec `Deploy-All.ps1` et rejouer le scénario de login sur le poste de test.
- **Compatibilité des hachages existants** : `PasswordHasher.Hash` reproduit l'algorithme à
  l'identique (mêmes octets d'entrée, même clé, même HMACSHA512), mais **non testé contre une vraie
  ligne `P_UTILISATEUR`** (pas de base réelle disponible ici) — à confirmer par un login réel réussi
  sur le poste de test avec un compte existant, pas seulement par relecture du code.
- Le mapping Dapper (`UtilisateurRow`) suppose `UT_HASH`/`UT_SALT` en `varbinary` (mappable
  directement en `byte[]`) — cohérent avec l'usage `SequenceEqual`/`Buffer.BlockCopy` déjà présent
  dans le code original, mais non vérifié contre le schéma réel de la table.
- Champ signalé, non traité (hors périmètre de cette demande) : `HashKey` (instance `HMACSHA512`
  statique partagée) n'est pas thread-safe pour des appels concurrents à `ComputeHash` — défaut déjà
  présent à l'identique dans le code legacy d'origine, reproduit ici sans modification volontaire.
  Risque théorique en cas de logins simultanés à très haute fréquence ; à signaler si le PO souhaite
  un correctif (verrou ou instance par appel), non demandé explicitement ici.

### Réserves — état après ce complément

- Cohabitation `System.Data.SqlClient`/`Microsoft.Data.SqlClient` dans `Declaration.API` :
  **éliminée par construction, vérifiée par inspection du publish self-contained**. Plus aucune trace
  d'un second provider SQL natif.
- Cause exacte du crash originel sur le poste de test : jamais formellement identifiée (l'hypothèse
  SNI a été affaiblie par le complément (nonies), pas confirmée) — mais la source du risque
  (cohabitation) est désormais supprimée à la racine, indépendamment de la cause exacte.
- **Non vérifié en conditions réelles** : rejeu du login sur le poste de test avec ce build, seul test
  qui tranchera définitivement.
- Tous les autres points ouverts des compléments précédents (Install()/SCM, encodage, valeur du
  subject ApLicence, port/mise à jour, retry-on-lock vs poste réel, copie non atomique vs poste réel,
  réserves n°1/n°3, gouvernance/revue indépendante) restent **inchangés**. TASK-115 reste en
  `IN_PROGRESS/`, VERIFY non clos.

## Complément du 19/07/2026 (undecies) — vérification en conditions réelles : login OK contre la vraie base

**Auto-évaluation, pas une revue indépendante.** Le PO a indiqué un accès réseau réel depuis ce poste
vers le serveur SQL cible (`connections.json` → `Server=DESKTOP-5BFKKEP`, joignable en TCP/1433 depuis
ce poste via un adaptateur Hyper-V). Ceci a permis, pour la première fois dans cette investigation,
un test qui n'est plus une simulation.

### Tests effectués contre le vrai serveur

1. **`scratch/sqlclient-repro-publish.zip`** (Microsoft.Data.SqlClient seul, complément octies) lancé
   avec la vraie chaîne `GrfConnection` (mot de passe jamais affiché dans mes commandes — extrait
   programmatiquement de `connections.json` par un script, jamais tapé en clair) :
   **`SUCCES : connexion ouverte. ServerVersion=16.00.1190`** (SQL Server 2022). Confirme que
   `Microsoft.Data.SqlClient` seul fonctionne parfaitement contre le vrai serveur.
2. **Build réel du correctif (decies)**, publié self-contained (`dotnet publish ... -r win-x64
   --self-contained true`), **exécuté** (`Declaration.API.exe`, port 5000, licence ApLicence validée
   au démarrage : `licence valide pour '/LIC/TRESO_GRF_COM'`) contre la vraie base
   `GR_EMA_DISTRIBUTION`.
3. **`POST /api/Auth/login`** avec un compte réel existant (`Admin`, confirmé actif via
   `SELECT UT_Login, UT_Admin, UT_Actif FROM P_UTILISATEUR` — seuls des noms de connexion consultés,
   jamais de hash/mot de passe) :
   - Mot de passe volontairement erroné → **`401 Identifiants invalides.`**, réponse propre, **aucun
     crash, aucune trace d'exception dans les logs serveur** (juste avant : démarrage propre,
     `Now listening on: http://[::]:5000`).
   - Ceci prouve que la séquence complète s'exécute sans erreur contre la vraie base : ouverture de
     connexion `Microsoft.Data.SqlClient`, requête Dapper sur `P_UTILISATEUR`, appel à
     `PasswordHasher.Hash` (nouvelle implémentation native), comparaison de hash, rejet propre — **le
     chemin qui plantait avant (`Microsoft.Data.SqlClient` dans `Declaration.API`) fonctionne
     désormais de bout en bout sur le vrai serveur**.
   - Mot de passe réel non connu de moi (je n'ai pas cherché à le deviner au-delà d'un essai
     raisonnable sur l'ancien mot de passe du mock TASK-074, `"admin"`, qui a échoué normalement) :
     **la correspondance exacte du hash contre une vraie valeur `UT_HASH`/`UT_SALT` stockée reste
     donc non prouvée** — seul un login réussi avec le vrai mot de passe (connu du PO) le confirmerait.
4. Processus arrêté et dossier de publication temporaire supprimé après test (non commité, jamais
   destiné à persister).

### Ce que ce complément change

- La réserve majeure du complément (decies) — « non vérifié en conditions réelles » — est
  **partiellement levée** : le crash `Microsoft.Data.SqlClient` ne se reproduit plus contre le vrai
  serveur, sur un vrai login (mauvais mot de passe, mais tout le pipeline SQL s'exécute).
- Reste ouvert : confirmation d'un login **réussi** (bon mot de passe) pour valider la compatibilité
  exacte du hash `PasswordHasher` avec les valeurs déjà stockées — seul le PO peut fournir ou vérifier
  ceci avec un vrai compte et son vrai mot de passe.
- Tous les autres points ouverts (Install()/SCM, encodage, port/mise à jour, retry-on-lock/copie
  atomique en conditions de service réel, réserves n°1/n°3, gouvernance/revue indépendante) restent
  **inchangés**, non couverts par ce test (qui portait spécifiquement sur le login/SqlClient).
  TASK-115 reste en `IN_PROGRESS/`, VERIFY non clos.
