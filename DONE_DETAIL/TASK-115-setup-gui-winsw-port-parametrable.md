# TASK-115 — Setup GUI (WinForms) + service Windows via WinSW-x64 (install/mise à jour) + port paramétrable au setup

## Contexte
Demande PO (17/07/2026), en marge de l'installation prod en cours (cf. `LANCEMENT_DEV.md`,
TASK-044, TASK-114) : l'installation/mise à jour chez le client doit être **automatique** —
un seul exécutable avec un **formulaire** de saisie (connexions SQL, secret JWT, identifiants
SageOM, port), qui installe le service Windows via **WinSW-x64** (plutôt que `sc.exe` brut) et
sait détecter s'il s'agit d'une **première installation ou d'une mise à jour** d'une installation
existante. Clarifications obtenues du PO pendant cette session :
- Formulaire = **exe GUI WinForms/WPF**, pas un script PowerShell ni une page web de setup.
- « Gestion de versionnement » ne signifie **pas** rollback multi-version : le setup doit juste
  **détecter install vs mise à jour** et agir en conséquence — pas de dossier `releases/` historisé.
- Le port (actuellement 5000, codé nulle part explicitement — c'est le défaut Kestrel) doit devenir
  **paramétrable dans le formulaire de setup**, et non plus quelque chose à éditer à la main dans
  `appsettings.json` (qui aujourd'hui ne définit d'ailleurs aucune clé `Urls`).

Vérification code réel : `Declaration.API/Program.cs` n'a **aucune** configuration de port/`Urls` —
Kestrel tourne sur son port par défaut. `LANCEMENT_DEV.md` §D mentionne à tort « le port défini
dans `appsettings.json` → `"Urls"` » (référence à corriger, cette clé n'existe pas dans le repo).

**Précision PO (17/07/2026, cette session) sur le produit installé** : l'application n'est pas
« Declaration TVA » mais **« Déclaratif Maroc »** (nom déjà retenu et appliqué en façade, cf.
TASK-093) — la **TVA n'est qu'un premier module**, un second module **« Déclaration Délai de
Paiement Fournisseur »** est déjà annoncé. Conséquence directe pour ce TASK : le setup GUI installe
le **produit** (un seul service, un seul dossier, TASK-044/TASK-116), pas un module — son nommage
(titre de fenêtre, nom de service Windows enregistré via WinSW, dossier d'installation par défaut)
doit utiliser **« Déclaratif Maroc »**, jamais « TVA » ni « Declaration » seuls, pour ne pas devenir
trompeur dès l'arrivée du 2ᵉ module. Voir aussi le point de vigilance déjà noté dans TASK-093 (§,
distinction produit/module reportée à « quand un 2ᵉ module réel sera livré ») — ce moment arrive.

## Périmètre STRICT
- **Inclus** :
  1. **Port paramétrable** : `Program.cs` lit le port depuis une section dédiée de `connections.json`
     (ex. `ServerConfig.Port`), défaut `5000` si absente (rétro-compat dev), et l'applique via
     `builder.WebHost.UseUrls(...)`. Le setup GUI écrit cette valeur — plus besoin d'éditer
     `appsettings.json` après coup.
  2. **Vendorisation WinSW-x64** : binaire (ou script de récupération de la release officielle) +
     gabarit XML de configuration du service (id, nom, executable `Declaration.API.exe`,
     `startmode=Automatic`, redémarrage sur crash, redirection logs).
  3. **Nouveau projet `Declaration.Setup`** (GUI WinForms/WPF, TFM Windows) :
     - **Détection install vs mise à jour** basée sur un signal univoque (ex. présence du service
       WinSW enregistré, ou d'un `connections.json` existant dans le dossier cible) — à spécifier
       précisément avant codage.
     - **Mode install** (dossier cible vide) : formulaire complet (connexions `GrfConnection` /
       `SageConnection` / `PersistenceConnection`, identifiants `SageOM`, secret JWT avec bouton
       « générer aléatoirement », port, **licence ApLicence : subject (obligatoire, pas de défaut) +
       adresse/port du serveur `ApLicence.Server` (cf. TASK-117 — pré-remplis `127.0.0.1` / `8003` à
       titre de valeur par défaut, modifiable — tranché PO 17/07/2026 : adresse/port **non uniques**
       pour tout le parc, donc vrais champs éditables, pas des constantes figées)**, dossier cible) →
       écrit `connections.json` → copie les binaires → installe et démarre le service via WinSW.
     - **Mode mise à jour** (installation existante détectée) : formulaire pré-rempli avec les
       valeurs actuelles lues dans le `connections.json` en place, stoppe le service, **préserve**
       la configuration (sauf champs explicitement modifiés), remplace les binaires, redémarre.
     - **Champs secrets (mots de passe SQL, SageOM, JWT) jamais pré-remplis en clair** en mode mise
       à jour : afficher masqué/vide avec indicateur « inchangé », n'écrire dans `connections.json`
       que si l'utilisateur modifie explicitement le champ.
     - **Pied de fenêtre** : mention fixe **« © {année} APBS Groupe — Tous droits réservés »**
       (PO, 17/07/2026) — texte statique, non paramétrable, visible sur l'écran de setup.
  4. **Intégration dans `deploy/`** : `Declaration.Setup.exe` + WinSW-x64 + gabarit XML packagés par
     `publish.ps1` (TASK-044) aux côtés de l'API/worker/front — un seul exe à lancer chez le client.
  5. **`DOCS/DEPLOIEMENT.md`** mis à jour : les étapes manuelles B/C/D de TASK-044 (copie à la main,
     `notepad connections.json`, `sc.exe create`) sont remplacées par « lancer `Declaration.Setup.exe`
     et remplir le formulaire ».
  6bis. **Refonte en assistant multi-écrans (wizard), style Inno Setup** (retour PO 18/07/2026, après
     pilotage réel du formulaire actuel sur poste Windows) : le formulaire `SetupForm` actuel
     (`TabControl` à 3 onglets tous accessibles librement — Connexions SQL / Sage & Licence
     ApLicence / Service Windows — plus dossier d'installation et prérequis toujours visibles en
     tête/pied) est jugé insatisfaisant par le PO, qui référence explicitement un précédent projet
     construit avec **Inno Setup** (assistant séquentiel, un écran = une étape, navigation
     « Suivant »/« Précédent »). **Périmètre tranché (PO, question de cadrage architecte, 18/07/2026)
     : tout le formulaire**, pas seulement les 3 groupes actuellement en onglets — chaque section
     devient une étape séquentielle du wizard :
     1. Dossier d'installation (+ détection install/mise à jour affichée)
     2. Connexions SQL (GRF + Persistance)
     3. Sage & Licence ApLicence
     4. Service Windows (port)
     5. Prérequis (statut .NET Framework 4.8 / Sage OM + confirmation)
     6. Récapitulatif final (relecture des valeurs saisies avant action) + bouton
        Installer/Mettre à jour (remplace `_btnInstallUpdate` en pied de formulaire unique)
     - Navigation **Suivant/Précédent** avec indicateur d'étape (ex. « Étape 2/6 ») ; validation des
       champs obligatoires de l'étape courante avant de pouvoir avancer (pas de validation reportée
       en bloc au clic final comme aujourd'hui, `OnInstallOrUpdateAsync`).
     - Le bandeau de marque (`BuildHeaderPanel`) et le pied de page (copyright, TASK-122) restent
       fixes, visibles sur toutes les étapes du wizard.
     - Portée = **refonte de présentation/navigation uniquement** : aucune logique métier
       (`ConnectionsFileService`, `WinSwServiceManager`, `PrerequisiteChecker`, etc.) n'est modifiée,
       seule `SetupForm` (structure de l'UI) est concernée.
  6. **Vérification/installation des prérequis runtime** (réponse à la question PO « les dépendances
     peuvent être installées automatiquement ? ») — traitement différencié, pas un « tout automatique
     » uniforme :
     - **.NET Runtime (API)** : éliminé par construction — `Declaration.API` doit être publié en
       **self-contained** (TASK-044, `dotnet publish --self-contained`) plutôt qu'en
       framework-dependent. Zéro dépendance à vérifier ou installer pour l'API.
     - **.NET Framework 4.8 (worker `SageTaxReader.Console.exe`)** : présent par défaut sur
       Windows 10/11 récents. Le setup **vérifie sa présence** (registre) au lancement ; si absent,
       propose l'installation **silencieuse** du redistribuable officiel Microsoft (vendorisé ou
       téléchargé) — nécessite les droits admin déjà requis pour l'installation du service, et peut
       exiger un redémarrage Windows (à signaler explicitement à l'utilisateur, pas une install
       « automatique et invisible »).
     - **Sage OM (composants COM Sage 100)** : **ne peut pas être installé automatiquement** — logiciel
       tiers sous licence Sage, doit déjà être installé/configuré sur la machine cible. Le setup se
       limite à **vérifier sa présence** (registre / enregistrement COM) et à bloquer avec un message
       clair si absent, plutôt que de terminer une installation « réussie » dont le worker plantera
       à l'usage.
       ⚠️ **Ne pas s'arrêter à « présent/absent »** : il faut aussi déterminer/sélectionner la
       **version Sage** installée (v7/v9/v10/v11/v12 — **v8 prévue mais non disponible pour le
       moment**, cf. TASK-101 — multi-version du worker). Le
       setup doit soit **détecter** la version via le composant COM enregistré, soit (si détection non
       fiable) proposer un **champ de sélection** dans le formulaire, et écrire dans
       `connections.json` le `WorkerConfig.WorkerExePath` correspondant à l'exécutable worker de la
       bonne version (`SageTaxReader.Console.vX.exe`, produit par TASK-101). Sans ça, le setup peut
       « réussir » tout en pointant vers un worker incompatible avec la base réelle du client
       (exactement l'incident déjà rencontré et documenté dans TASK-101, `0xFFFFF562`).
- **Exclu** :
  - Le contenu du dossier `deploy/` (build front + `dotnet publish` API + worker net48) reste
    **TASK-044** — TASK-115 consomme ce dossier, ne le construit pas.
  - **Rollback multi-version / historisation de releases** — explicitement écarté par le PO
    (clarification session 17/07/2026) : le setup gère install et mise à jour, pas un retour en
    arrière automatisé.
  - HTTPS/certificats — hors périmètre, HTTP simple sur le port choisi, cohérent avec l'existant
    (pas de `UseHttpsRedirection` aujourd'hui).
  - Interface web de setup — option écartée par le PO au profit du GUI natif.

## Objectif
```
Entrée  : dossier deploy/ (TASK-044, sans installateur), sc.exe manuel, port Kestrel non
          paramétrable, aucun formulaire de saisie
Étapes  : port lu depuis connections.json + WinSW-x64 vendorisé + projet Declaration.Setup
          (détection install/maj, formulaire, écriture connections.json, invocation WinSW)
Sortie  : un seul exe chez le client, lancé à l'install comme à chaque mise à jour, qui configure
          et (re)démarre le service Windows sans étape manuelle
```

## Étapes
1. **`Program.cs`** : ajouter la lecture du port (`ServerConfig.Port` ou clé équivalente dans
   `connections.json`), `builder.WebHost.UseUrls($"http://+:{port}")`, défaut 5000.
2. **Vendoriser WinSW-x64** (release officielle MIT) + gabarit XML de service (nom de service,
   chemin exe, politique de redémarrage, chemin logs).
3. **Projet `Declaration.Setup`** : détection install/mise à jour (signal à spécifier), formulaire,
   écriture/relecture de `connections.json`, orchestration `winsw install`/`stop`/`start`, copie des
   binaires en préservant `connections.json` en mode mise à jour.
4. **Intégrer au packaging** : étendre `publish.ps1` (TASK-044) pour inclure
   `Declaration.Setup.exe` + WinSW + gabarit XML dans `deploy/`.
5. **Mettre à jour `DOCS/DEPLOIEMENT.md`** et `LANCEMENT_DEV.md` §D (retirer la référence erronée à
   `appsettings.json` → `"Urls"`, décrire le setup GUI).

## Livrables
- `Program.cs` modifié (port paramétrable via `connections.json`).
- WinSW-x64 vendorisé + gabarit XML de service.
- Projet `Declaration.Setup` (GUI) livré et fonctionnel.
- `publish.ps1` (TASK-044) étendu.
- `DOCS/DEPLOIEMENT.md` + `LANCEMENT_DEV.md` à jour.
- `VERIFY/TASK-115_verify.md` : preuve réelle —
  - install à blanc dans un dossier vide : service créé, démarré, API répond sur le port saisi
    dans le formulaire ;
  - relance du même exe sur une installation existante : mode « mise à jour » détecté sans
    intervention manuelle, `connections.json` existant préservé (sauf champs modifiés), binaires
    remplacés, service redémarré ;
  - port déjà occupé par un autre process : message d'erreur clair dans le formulaire, pas de
    crash silencieux ni de service créé dans un état incohérent.

## Critères de validation
- Un seul exe (`Declaration.Setup.exe`) couvre install **et** mise à jour, sans étape manuelle
  `sc.exe`/`notepad`.
- Port choisi au formulaire, plus jamais à chercher/éditer dans `appsettings.json`.
- Service Windows géré par WinSW (pas par `sc.exe` brut) — redémarrage sur crash configuré.
- `connections.json` reste le seul fichier de configuration applicative ; le setup automatise
  seulement son écriture, il n'introduit pas de second mécanisme de config parallèle.
- Aucun rollback multi-version requis (hors périmètre, cf. clarification PO).
- Nommage visible côté client (titre fenêtre setup, nom du service Windows enregistré via WinSW,
  dossier d'installation par défaut) = **« Déclaratif Maroc »** (produit), jamais « TVA » seul.
- Formulaire restructuré en **assistant séquentiel (wizard)** couvrant tout le formulaire (6 étapes,
  cf. point 6bis) — plus de `TabControl` à onglets librement navigables ; navigation
  Suivant/Précédent avec indicateur d'étape, validation par étape, écran récapitulatif avant action
  finale. Capture d'écran de **chaque étape** apportée en preuve réelle (cohérent avec la réserve
  n°6 déjà ouverte sur le formulaire actuel).

## Risques / dépendances
- **Nom de service Windows existant à harmoniser** : le service actuel s'appelle `DeclarationTVA`
  (cf. `sc.exe stop/start DeclarationTVA` dans TASK-116) — nommage TVA-only qui deviendra trompeur
  dès la livraison du 2ᵉ module (« Délai de Paiement Fournisseur », annoncé 17/07/2026). Ce TASK-115
  ne doit pas reconduire ce nom : choisir un identifiant de service générique produit (ex.
  `DeclaratifMaroc`) pour toute **nouvelle** installation via WinSW. Le renommage du service
  **existant déjà installé** (migration d'un nom à l'autre) est une question distincte, à cadrer
  séparément si des installations `DeclarationTVA` sont déjà en prod au moment de livrer TASK-115 —
  pas tranché ici, à signaler au PO le moment venu.
- **Dépend de TASK-044** : nécessite un dossier `deploy/` stable et complet (worker net48 copié,
  `publish.ps1` fonctionnel) — TASK-115 empaquette et installe ce dossier, ne le produit pas.
- **Licence WinSW (MIT)** — vérifier avant de committer le binaire dans le dépôt ; alternative :
  script de téléchargement de la release officielle au moment du `publish.ps1` plutôt que binaire
  vendorisé en dur.
- **Fiabilité de la détection install/mise à jour** : point le plus sensible du projet — un faux
  « mise à jour » pourrait écraser un `connections.json` valide, un faux « install » pourrait casser
  une installation existante. Le signal de détection doit être choisi et documenté avant codage,
  pas deviné en cours de route (cf. règle CLAUDE.md : ne jamais improviser un contexte manquant).
- **Redondance partielle avec `UseWindowsService()`** (déjà présent dans `Program.cs`) : WinSW
  apporte surtout la configuration déclarative du redémarrage sur crash et la redirection des logs,
  que `sc.exe`/SCM natif peuvent aussi couvrir (`sc failure`) sans dépendance supplémentaire — à
  noter pour le PO, mais WinSW reste retenu suite à la demande explicite.
- **Vérification de disponibilité du port** avant installation, pour éviter un service créé mais
  qui échoue silencieusement au démarrage.
- **Sage OM hors de portée de l'automatisation** : dépendance à un logiciel tiers sous licence
  (Sage 100), installé/maintenu par le client — le setup ne peut que détecter son absence, jamais
  l'installer. À documenter clairement pour éviter toute attente côté PO/client d'une installation
  « tout automatique ».
- **Dépend de TASK-101** (support multi-version worker Sage, actuellement TODO/HIGH) : le champ
  « version Sage » du formulaire de setup et le calcul du `WorkerExePath` écrit dans
  `connections.json` n'ont de sens que si TASK-101 a produit les variantes `SageTaxReader.Console.vX.exe`.
  Si TASK-115 est livré avant TASK-101, prévoir a minima le champ dans le formulaire (sélection
  manuelle v7/v9/v10/v11/v12 — **v8 grisée/marquée « non disponible » dans le formulaire** tant
  qu'aucun DLL interop v8 n'est fourni) pointant vers l'unique exécutable existant en attendant —
  pas de détection automatique tant que les variantes n'existent pas.
- **Publication self-contained (TASK-044)** : à coordonner avec TASK-044 — impact sur la taille du
  dossier `deploy/` (runtime .NET embarqué), aucun changement fonctionnel.
- **Indépendant de TASK-114** (JWT/droits SQL) — le formulaire de setup saisit le secret JWT et les
  identifiants SQL, mais ne change rien au mécanisme de garde-fou déjà livré dans TASK-114.
- **Retour tardif de PO sur une UI déjà implémentée** (point 6bis, 18/07/2026) : le `TabControl`
  actuel est **code-complet et buildé** (cf. `VERIFY/TASK-115_verify.md`) — la refonte en wizard est
  une reprise de `SetupForm.cs` (structure de layout + gestion de la navigation), pas un ajout net.
  Ne pas re-designer silencieusement `ConnectionGroup`/services : seule la coquille `SetupForm`
  change de forme (TabControl → panneaux séquentiels + `_currentStep`).
- **Champs « licence ApLicence » (subject + adresse/port serveur) dépendants de TASK-117** (intégration
  ApLicence dans `Declaration.API`, elle-même bloquée par la livraison de la librairie du repo
  `GRLicence`) : ces champs du formulaire n'ont d'effet réel qu'une fois TASK-117 implémenté (clés
  lues par `Program.cs`). Peuvent être ajoutés au formulaire dès maintenant (champs texte simples,
  valeurs écrites dans `connections.json`), mais resteront sans effet fonctionnel jusqu'à TASK-117.
  **Exigence PO (17/07/2026)** : ces valeurs se saisissent **uniquement** via ce formulaire de setup,
  jamais en éditant `connections.json` à la main — cohérent avec le principe déjà posé pour le port
  (ligne 33) et tous les autres champs de ce TASK. **Tranché (PO, 17/07/2026)** : adresse/port du
  serveur de licence **ne sont pas uniques pour tout le parc** (varient par client/environnement) —
  champs éditables avec valeur par défaut pré-remplie `127.0.0.1` / `8003`, à corriger par
  l'installateur si différente chez le client. Le `subject`, lui, reste sans valeur par défaut
  (saisie obligatoire, fail-closed cf. CDC §5).
