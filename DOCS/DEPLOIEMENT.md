# Déploiement — Déclaratif Maroc

> Ce document décrit **l'installation/mise à jour chez le client** (TASK-115). La production du
> dossier `deploy\` lui-même (build front + `dotnet publish` API + build workers Sage net48) reste
> documentée dans `LANCEMENT_DEV.md` (§ Déploiement) — **TASK-044 n'a pas livré de `publish.ps1`
> unique à ce jour** ; `deploy\` est aujourd'hui assemblé via les commandes manuelles de
> `LANCEMENT_DEV.md` §A/§A2, puis complété par `Declaration.Setup\deploy\Publish-Setup.ps1`
> (TASK-115, ajoute `DeclaratifMaroc.exe` + WinSW). Ce document ne couvre **que** l'étape
> d'installation chez le client, une fois `deploy\` prêt.

## Pré-requis avant de livrer `deploy\` au client

- `deploy\Declaration.API.exe` et ses dépendances (self-contained recommandé, cf. TASK-115
  §Périmètre point 6 — élimine la dépendance au runtime .NET côté client).
- `deploy\wwwroot\` (front buildé).
- `deploy\workers\v7|v9|v10|v12\` (variantes worker Sage, TASK-101).
- `deploy\DeclaratifMaroc.exe` (setup, ex-`Declaration.Setup.exe`, TASK-119) + `deploy\WinSW\`
  (gabarit XML + `WinSW.exe` — voir `Declaration.Setup\WinSW\Get-WinSW.ps1` pour récupérer ce
  binaire, licence MIT, non committé).
- **Aucun** `connections.json` réel ne doit être committé/livré dans le dépôt — c'est le setup qui
  l'écrit chez le client.

## Installer une nouvelle instance

1. Copier le dossier `deploy\` complet sur la machine cible (ou le lancer depuis un partage/clé USB).
2. Lancer `DeclaratifMaroc.exe` (droits administrateur requis — installation d'un service Windows).
3. Choisir un **dossier cible vide** (défaut proposé : `C:\DeclaratifMaroc`) → le setup détecte
   **Installation** (aucun `connections.json` existant à cet emplacement).
4. Remplir le formulaire :
   - **Connexions SQL** (GRF / Persistance) : serveur, base, utilisateur, mot de passe. La
     connexion Sage n'est **plus** saisie ici (TASK-119) : elle est résolue dynamiquement par
     société (`SO_Id` → `P_SOCIETE.SO_ErpDb`/`SO_ErpUserApp`/`SO_ErpPasswdApp`, TASK-118), donc
     hors du périmètre du setup.
   - **Version Sage 100 installée** (v7 / v9 / v10-v11 / v12 — v8 non disponible, TASK-101).
   - **Secret JWT** : bouton « Générer aléatoirement » (obligatoire à l'installation).
   - **Port d'écoute HTTP** (défaut 5000) — le setup vérifie sa disponibilité avant de continuer.
   - **Licence ApLicence** (subject obligatoire sans défaut, adresse/port serveur — défaut
     `127.0.0.1`/`8003`, modifiables) : sans effet fonctionnel tant que TASK-117 n'est pas livrée,
     mais doit être saisi **uniquement** ici, jamais à la main dans `connections.json`.
5. Valider : le setup
   - vérifie la présence de **.NET Framework 4.8** (registre) — propose une installation
     silencieuse du redistribuable officiel Microsoft si absent (redémarrage Windows possible,
     signalé explicitement) ;
   - affiche un état **« indétectable automatiquement »** pour **Sage OM** (aucun ProgID/CLSID
     confirmé par le PO à ce jour — cf. réserve ci-dessous) et demande une confirmation manuelle
     explicite avant de continuer ;
   - copie les binaires dans le dossier cible ;
   - écrit `connections.json` ;
   - installe le binaire WinSW sous le nom `DeclaratifMaroc.exe`/`.xml`, puis exécute
     `DeclaratifMaroc.exe install` et `start`.
6. Vérifier : `http://localhost:<port choisi>` répond (front + `GET /api/...`).

## Mettre à jour une instance existante

1. Lancer `DeclaratifMaroc.exe` et choisir le **dossier de l'installation existante** → le
   setup détecte **Mise à jour** (un `connections.json` y est déjà présent) et pré-remplit le
   formulaire avec les valeurs actuelles.
2. **Les champs secrets (mots de passe SQL, JWT) sont toujours affichés vides** avec
   l'indicateur « inchangé si laissé vide » — les laisser vides préserve la valeur existante ; les
   renseigner les remplace.
3. Valider : le setup arrête le service (`DeclaratifMaroc.exe stop`), copie les nouveaux binaires
   en **préservant** `connections.json` (sauf les champs explicitement modifiés), puis redémarre
   le service.

## Nom du service Windows

Le service installé par `DeclaratifMaroc.exe` s'appelle **`DeclaratifMaroc`** (nom **produit**,
pas module — cf. TASK-093/TASK-115 : la TVA n'est qu'un premier module, un second module
« Délai de Paiement Fournisseur » est déjà annoncé). Une installation antérieure sous l'ancien nom
`DeclarationTVA` (`sc.exe`, avant TASK-115) n'est **pas** migrée automatiquement — question
distincte, à cadrer avec le PO si un tel service existe déjà en production au moment de la
livraison de TASK-115.

## Réserves connues (à lire avant toute installation en production)

- **Détection Sage OM non fiable automatiquement** : aucun ProgID/CLSID confirmé par le PO à ce
  jour pour vérifier par registre/COM la présence réelle des composants Sage 100. Le setup ne
  bloque donc pas silencieusement sur un faux positif — il demande une confirmation manuelle
  explicite. Ne pas improviser cette détection tant que le PO n'a pas fourni l'identifiant exact.
- **`publish.ps1` (TASK-044) non livré** : `deploy\` est aujourd'hui assemblé à la main
  (`LANCEMENT_DEV.md` §A/§A2) + `Declaration.Setup\deploy\Publish-Setup.ps1` (TASK-115). Une fois
  TASK-044 livrée, son script devra appeler/englober `Publish-Setup.ps1` plutôt que dupliquer la
  logique d'assemblage.
- **Rollback multi-version hors périmètre** (décision PO 17/07/2026) : le setup gère install et
  mise à jour, pas un retour en arrière automatisé.
- **Identifiants Sage OM par société** (`SO_ErpUserApp`/`SO_ErpPasswdApp`) : vivent désormais dans
  `P_SOCIETE`, gérés par l'application principale — le setup GRF ne les saisit plus (TASK-119,
  suite structurelle de TASK-118).
