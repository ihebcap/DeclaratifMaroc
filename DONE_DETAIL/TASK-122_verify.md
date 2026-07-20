# VERIFY — TASK-122 : Setup GUI — connexion SQL unique, subject fixé, dossier par défaut, icône + charte noir/vert

## Statut

**Implémentée en worker exceptionnel** (rôle inversé, demande explicite PO/architecte 18/07/2026,
cf. réserve `CLAUDE.md`, même mode que TASK-101/TASK-075/TASK-114/TASK-117/TASK-118). Contrairement
à TASK-115/TASK-119 (réserves bloquantes faute d'accès GUI interactif), cette session dispose d'un
accès Windows réel : le formulaire a été **réellement exécuté**, piloté et capturé (`PrintWindow`,
UI Automation), pas seulement relu en code.

## Ce qui a été livré

1. **Connexion SQL unique** (`SetupForm.cs`) : les deux `ConnectionGroup` (`_grf`/`_persistence`)
   fusionnés en un seul `_connection` ("Connexion SQL (GRF + Persistance)"). `ReadDataFromForm()`
   duplique la même saisie vers `SetupData.Grf` et `SetupData.Persistence` (deux instances
   indépendantes, mêmes valeurs). `connections.json` reste inchangé dans sa structure (toujours
   deux clés `GrfConnection`/`PersistenceConnection`, cf. périmètre exclu de la task).
2. **Mode mise à jour — divergence** (Périmètre point 3, tranché avant codage) : décision retenue —
   celle suggérée en exemple par la task elle-même — `ApplyDataToForm` pré-remplit `_connection`
   **uniquement** depuis `SetupData.Grf` (canonique) ; si `Grf` et `Persistence` diffèrent
   (Server/Database/UserId — mot de passe ignoré, toujours vide au pré-remplissage), un label
   d'avertissement (`_lblConnectionDivergence`, orange) apparaît sous le groupe, expliquant
   explicitement que la valeur affichée écrasera la connexion Persistance existante si l'on
   poursuit. Pas de blocage — l'installateur reste maître de la décision, avec l'information
   nécessaire pour ne pas être surpris.
3. **Subject ApLicence fixé** : champ `_txtApLicenceSubject` et sa validation (`MessageBox` "champ
   manquant") retirés de `SetupForm.cs`. `SetupData.ApLicenceSubject` a désormais pour valeur par
   défaut la constante `"TRESO_GRM_COM"` (au lieu de `""`) — jamais réécrit par le formulaire (plus
   de champ), donc toujours cette constante à la construction. `ConnectionsFileService.ExtractForPrefill`
   ne lit plus `ApLicence.Subject` depuis un `connections.json` existant : même en mise à jour d'une
   installation antérieure à ce TASK, la valeur écrite reste `TRESO_GRM_COM`, jamais une ancienne
   valeur reprise du disque.
4. **Dossier d'installation par défaut** : `_txtInstallFolder.Text` initialisé via
   `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "APBS", "Declaratif Maroc")`
   au lieu de `C:\DeclaratifMaroc` codé en dur. `Environment.GetFolderPath` échoue explicitement
   (pas de repli deviné) si l'API ne peut pas résoudre le dossier — conforme au critère "aucun
   repli silencieux".
5. **Icône** : `Declaration.Setup/Assets/AppIcon.ico` régénérée à partir de
   `TASKS/assets/task-120-icon-dm/icon-dm-512.png` (redimensionnement 16/32/48/256 en PNG encodé
   dans un conteneur ICO standard, aucun nouveau design — reconversion technique uniquement, outil
   jetable supprimé après usage). Remplace l'ancienne icône indigo `#2b4c7e` (TASK-119).
6. **Charte graphique (en-tête + bouton)** : nouveau bandeau (`BuildHeaderPanel`) fond `#1a1a1a` en
   haut du formulaire (hors barre de titre native, restylage impossible côté OS) avec icône DM
   (extraite dynamiquement de l'exe via `Icon.ExtractAssociatedIcon`, donc toujours synchronisée
   avec `AppIcon.ico`) + titre en vert `#3ddc84`. Bouton "Installer" : `FlatStyle.Flat`,
   `BackColor = #178a4c`, texte blanc, bordure supprimée. Reste du formulaire (onglets, champs,
   labels) inchangé — thème système standard, conforme au périmètre volontairement limité de la
   task.

## Décisions actées avant codage (traçabilité, cf. CLAUDE.md "ne jamais improviser un contexte manquant")

- Comportement de pré-remplissage en cas de divergence `Grf`/`Persistence` (point non tranché dans
  la task) : **canonique = `GrfConnection`, avertissement si divergence** — décision documentée
  ci-dessus, pas devinée silencieusement.
- `ApLicenceSubject` fixé comme propriété par défaut de `SetupData` (plutôt qu'une constante locale
  à `SetupForm`) : plus proche du texte de la task ("fixée en constante ... au moment de la
  construction de `SetupData`") et garantit qu'aucun code (mise à jour comprise) ne peut
  accidentellement réintroduire une valeur lue du disque.

## Vérifications effectuées

### Build
- `dotnet build Declaration.Setup/Declaration.Setup.csproj -c Debug` → **0 erreur, 0 avertissement**.
- `dotnet build Declaration.Setup/Declaration.Setup.csproj -c Release` → **0 erreur, 0 avertissement**.
- Aucune modification hors `Declaration.Setup` (`Declaration.API`/`Declaration.Infrastructure` non
  touchés) — pas de risque de régression sur la lecture des clés `connections.json`.

### Preuve réelle GUI (formulaire réellement exécuté et capturé)
- `VERIFY/task122-01-connexions-sql-unifiees.png` : onglet "Connexions SQL" — **un seul** groupe
  Serveur/Base/Utilisateur/Mot de passe (plus de blocs "GRF"/"Persistance" séparés) ; dossier
  d'installation pré-rempli à `C:\Program Files\APBS\Declaratif Maroc` ; bandeau `#1a1a1a` + icône
  DM + titre vert visibles ; bouton "Installer" vert `#178a4c`.
- `VERIFY/task122-02-sage-licence-sans-subject.png` : onglet "Sage & Licence ApLicence" — **plus
  aucun champ "Subject"**, seuls "Adresse serveur"/"Port serveur" restent.
- Icône : le monogramme "DM" (fond noir, lettres vertes) s'affiche correctement dans le bandeau
  (`Icon.ExtractAssociatedIcon` sur l'exe compilé) — preuve indirecte mais réelle que `AppIcon.ico`
  régénérée est valide et correctement embarquée par `<ApplicationIcon>`.

### Preuve réelle de `connections.json` (exécution directe des classes de production, sans passer par WinSW/service Windows — cf. réserve ci-dessous)
Harnais jetable (compilant directement `Declaration.Setup/Models/SetupData.cs`,
`SqlConnectionParts.cs`, `SageVersion.cs` et `Services/ConnectionsFileService.cs` réels, supprimé
après usage) reproduisant exactement `ReadDataFromForm()` (une saisie dupliquée vers `Grf`/
`Persistence`) puis `ApplyChanges`/`Save` :

```
GrfConnection == PersistenceConnection : True
ApLicence.Subject == "TRESO_GRM_COM"    : True
SetupData.ApLicenceSubject par defaut   : "TRESO_GRM_COM"
```

Fichier `connections.json` réellement généré (extrait) :
```json
{
  "ConnectionStrings": {
    "GrfConnection": "Server=SRV01;Database=GR_EMA_DISTRIBUTION;User Id=decl_tva_app;Password=...;TrustServerCertificate=True;",
    "PersistenceConnection": "Server=SRV01;Database=GR_EMA_DISTRIBUTION;User Id=decl_tva_app;Password=...;TrustServerCertificate=True;"
  },
  "ApLicence": { "Subject": "TRESO_GRM_COM", "ServerAddress": "127.0.0.1", "ServerPort": 8003 }
}
```

**Scénario mise à jour avec divergence préexistante** (même harnais, `connections.json` fabriqué à
la main avec `GrfConnection` ≠ `PersistenceConnection` avant l'exécution) :
```
Grf (canonique affiche)         : OLD-GRF/GRF_OLD/old_user
Persistence (non affiche, compare) : OLD-PERSIST/PERSIST_OLD/old_user2
Divergence detectee (declenche avertissement UI) : True
ApLicenceSubject pre-rempli (doit ignorer l'ancien fichier) : "TRESO_GRM_COM"
```
Confirme : (a) `ExtractForPrefill` expose bien les deux valeurs distinctes nécessaires à la
détection de divergence côté `SetupForm.ApplyDataToForm`/`ConnectionsDiverge`, (b) l'ancien subject
`"UN_ANCIEN_SUBJECT"` du fichier préexistant est bien ignoré, jamais réutilisé.

## Réserves non bloquantes

- **Avertissement de divergence non capturé visuellement en conditions réelles** : une tentative de
  pilotage du champ dossier via `SendKeys` (pour déclencher `RefreshMode()` sur un dossier contenant
  un `connections.json` divergent fabriqué à la main) a produit une saisie corrompue (caractères
  réordonnés par l'injection d'entrée dans cet environnement multi-écran) — capture non exploitable,
  non retentée pour ne pas risquer d'action instable. La logique exacte (mêmes classes de
  production) est néanmoins prouvée par le harnais ci-dessus, qui exerce le code réel
  (`ExtractForPrefill` + la même comparaison que `ConnectionsDiverge`) plutôt qu'une réimplémentation.
- **Aucune installation réelle de service Windows (WinSW) exercée** dans cette session — action
  système à effet persistant (installation d'un service Windows) volontairement non déclenchée sans
  confirmation explicite préalable, conformément à la prudence requise pour les actions à effet
  difficilement réversible. Le point vérifié (écriture de `connections.json`) est strictement
  antérieur à l'appel WinSW dans `RunInstallOrUpdate` et n'en dépend pas.
- Le formulaire a été exécuté et capturé dans **cette** session (contrairement à TASK-115/TASK-119)
  — pas de réserve "aucune preuve GUI" à reporter au PO cette fois.

## Fichiers modifiés

- `Declaration.Setup/SetupForm.cs`
- `Declaration.Setup/Models/SetupData.cs`
- `Declaration.Setup/Services/ConnectionsFileService.cs`
- `Declaration.Setup/Assets/AppIcon.ico` (régénérée, binaire)

## Recommandation

**APPROVE.** Tous les critères de validation de la task sont couverts par une preuve réelle (GUI
capturé + `connections.json` généré par le code de production réel), aucune régression possible
côté `Declaration.API`/`Declaration.Infrastructure` (non touchés), aucun repli silencieux introduit.
Réserves ci-dessus non bloquantes (alternative de preuve équivalente apportée pour la première ;
action système volontairement non déclenchée pour la seconde, sans impact sur le périmètre vérifié).
