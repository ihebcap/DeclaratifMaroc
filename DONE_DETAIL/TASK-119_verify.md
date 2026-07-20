# VERIFY — TASK-119 — Correction `Declaration.Setup` : retrait champs Sage devenus obsolètes (impact TASK-118) + icône + renommage exe

## Statut

Implémentation **code-complète** livrée (worker exceptionnel, cf. réserve `CLAUDE.md`). TASK-118
(bloqueur) est ✅ **done** (18/07/2026) — plus aucun blocage. Build `Declaration.Setup` (Debug +
Release) **0 erreur, 0 avertissement**. Comme pour TASK-115 : **aucun run réel du GUI sur poste
cible n'a été effectué dans cette session** (pas d'accès interactif Windows/UAC pour piloter une
fenêtre WinForms) — même limite d'environnement, documentée ci-dessous plutôt qu'improvisée.

## Ce qui a été livré

### 1. Retrait des champs Sage/SageOM (Périmètre §1-3)

| Fichier | Retiré |
|---|---|
| `Declaration.Setup/Models/SetupData.cs` | `SqlConnectionParts Sage`, `SageOmUser`, `SageOmPassword` — commentaire de classe mis à jour (mention explicite : connexion Sage résolue par `SO_Id`/TASK-118, plus jamais saisie ici). |
| `Declaration.Setup/SetupForm.cs` | Champ `_sage` (`ConnectionGroup` « Sage (commerciale) »), champs `_txtSageOmUser`/`_txtSageOmPassword` + groupe « Identifiants applicatifs Sage OM » (`BuildSageEtLicenceTab`) ; retrait de `_sage.GroupBox` dans `BuildConnectionsTab` ; retrait du pré-remplissage (`ApplyDataToForm`) et de la lecture au submit (`ReadDataFromForm`). |
| `Declaration.Setup/Services/ConnectionsFileService.cs` | Lecture `SageConnection`/`SageOM` dans `ExtractForPrefill` ; écriture `SageConnection`/`SageOM` dans `ApplyChanges`. Commentaire de méthode mis à jour (mots de passe SQL/JWT seulement). |

**Conservé volontairement** (hors périmètre strict de la tâche, distinct des identifiants de
connexion Sage) : `PrerequisiteChecker.CheckSageOm()` + `_lblSageOmStatus`/`_chkConfirmSageOm` —
c'est la case à cocher « Sage OM est installé sur cette machine » (prérequis logiciel, TASK-115),
sans rapport avec une connexion/un identifiant applicatif Sage. Le sélecteur de **version** Sage
100 (`_cboSageVersion`, `WorkerExePath`) est également conservé — hors périmètre (TASK-101), n'a
jamais été une connexion Sage.

`connections.json`/`connections.json.exemple` : aucun fichier `.exemple` n'existe dans le dépôt
(vérifié par recherche) ; le format `connections.json` réel n'est plus jamais généré avec les clés
`SageConnection`/`SageOM` par le setup — elles peuvent rester silencieusement absentes sans impact
(déjà ignorées côté `Declaration.API` depuis TASK-118, cf. `LANCEMENT_DEV.md` mis à jour ci-dessous).

### 2. Documentation (Périmètre §4)

- `DOCS/DEPLOIEMENT.md` : section « Installer une nouvelle instance » — bullet « Connexions SQL »
  limité à GRF/Persistance avec renvoi explicite à TASK-118/TASK-119 ; bullet « Identifiants
  applicatifs Sage OM » supprimé ; section « Mettre à jour » (mots de passe SQL/JWT, plus SageOM) ;
  nouvelle réserve « Identifiants Sage OM par société vivent dans `P_SOCIETE` ». Renommage exe
  (§3 ci-dessous) appliqué dans le même fichier.
- `LANCEMENT_DEV.md` : §« Connexion Sage dynamique par `SO_Id` » — le setup n'édite plus
  `SageConnection`/`SageOM` (mise à jour du constat, ces clés sont désormais **entièrement**
  obsolètes, pas seulement non lues côté API) ; §B/C/D/E — bullet 2 mis à jour (connexions SQL
  GRF/Persistance seulement).

### 3. Icône `.ico` (Périmètre §5)

`Declaration.Setup/Assets/AppIcon.ico` créé — multi-résolution **16/32/48/256**, format ICO
standard (header + répertoire + frames PNG, lisible Vista+). Design conforme à la proposition de
la task : monogramme **« DM »** blanc, police Segoe UI Bold, sur fond **indigo profond `#2b4c7e`**
(la variable `--accent-primary` de TASK-083), carré à coins arrondis (rayon ≈22 % du côté) —
identique à l'esprit du monogramme sidebar (`App.tsx`, TASK-083). Générée par un petit programme
C#/`System.Drawing` jetable (supprimé après usage, non committé), pas un asset dessiné à la main.

**Preuve d'intégration réelle** (pas seulement présence du fichier) : après build Release,
extraction de l'icône associée à `DeclaratifMaroc.exe` via `Icon.ExtractAssociatedIcon` (outil
jetable séparé) → icône **32×32 non générique** extraite avec succès, rendu confirmé visuellement
identique au design (DM blanc sur indigo, coins arrondis). Si l'icône n'avait pas été embarquée
correctement, Windows aurait renvoyé l'icône générique de l'exécutable.

### 4. Renommage de l'exe livré au client (Périmètre §6)

`Declaration.Setup.csproj` : `<AssemblyName>Declaration.Setup</AssemblyName>` →
`<AssemblyName>DeclaratifMaroc</AssemblyName>` (recommandation « sans espace » retenue, cohérente
avec l'id de service WinSW `DeclaratifMaroc` déjà choisi en TASK-115 — pas de décision PO contraire
signalée). `<ApplicationIcon>` pointé vers `Assets\AppIcon.ico`.

Les **6 fichiers** référençant `Declaration.Setup.exe` en dur (grep confirmé avant/après) mis à jour :

| # | Fichier | Nature du changement |
|---|---|---|
| 1 | `Deploy-All.ps1` | Docstring (§Description, étapes 4-7, résultat final) + 4 `Write-Host` + filtre `robocopy /XF` (`Declaration.Setup.*` → `DeclaratifMaroc.*`). |
| 2 | `Declaration.Setup/deploy/Publish-Setup.ps1` | Docstring (`.SYNOPSIS`, corps) + `Write-Host` final. |
| 3 | `Declaration.Setup/Services/DeploymentCopier.cs` | Commentaire de classe + **filtre d'exclusion effectif** `fileName.StartsWith("Declaration.Setup", ...)` → `StartsWith("DeclaratifMaroc", ...)` (sans ce correctif, l'installeur aurait copié ses propres binaires renommés dans le dossier cible — bug réel, pas seulement cosmétique). |
| 4 | `Declaration.Setup/Services/WinSwServiceManager.cs` | 2 commentaires (`PrepareServiceFiles`) — `ServiceId`/`ServiceName` déjà `DeclaratifMaroc` depuis TASK-115, aucune logique à changer. |
| 5 | `DOCS/DEPLOIEMENT.md` | Toutes les occurrences `Declaration.Setup.exe` → `DeclaratifMaroc.exe` (installer/mettre à jour/nom du service), une mention historique explicite conservée (« ex-`Declaration.Setup.exe` »). |
| 6 | `LANCEMENT_DEV.md` | Titre de section + commande résumée + une mention historique explicite conservée. |

## Vérifications effectuées

- `dotnet build Declaration.Setup/Declaration.Setup.csproj -c Debug` → **0 erreur, 0 avertissement**, sortie `DeclaratifMaroc.dll`/`.exe`.
- `dotnet build Declaration.Setup/Declaration.Setup.csproj -c Release` → **0 erreur, 0 avertissement**, sortie `DeclaratifMaroc.exe` confirmée sur disque (`bin/Release/net10.0-windows/DeclaratifMaroc.exe`).
- `Declaration.Setup` n'est référencé par aucun autre projet du `.slnx` (vérifié) — pas de rebuild solution nécessaire, pas de risque de régression croisée.
- Grep exhaustif `Declaration\.Setup\.exe` sur tout le dépôt après correctif : les seules occurrences restantes sont (a) des mentions historiques explicites (« ex-`Declaration.Setup.exe` ») dans les fichiers mis à jour, ou (b) des documents figés hors périmètre (`TASKS/TASK-119-*.md` = cette task elle-même, `IN_PROGRESS/TASK-115-*.md` et `VERIFY/TASK-115_verify.md` = archives d'une tâche déjà tranchée, `TODO.md` = ligne de suivi PO). Aucune occurrence active oubliée dans le code/scripts/doc courants.
- Grep `SageOm|"Sage (commerciale)"` sur `Declaration.Setup/` : plus aucune occurrence de saisie/lecture/écriture de connexion ou d'identifiant Sage — seules restent les mentions **prérequis logiciel** (`CheckSageOm`, case de confirmation) et **version Sage** (hors périmètre, cf. ci-dessus).
- Icône : extraction réelle depuis l'exe buildé (cf. §3), pas seulement vérification du fichier source.

## Critères de validation (repris de la task)

- [x] Le formulaire de setup ne comporte plus aucun champ Sage/SageOM (ni saisie, ni pré-remplissage, ni écriture dans `connections.json`).
- [x] Aucune régression sur les autres champs du formulaire (port, WinSW, JWT, ApLicence, version Sage — tous inchangés, non touchés par cette task).
- [x] L'exe livré au client porte le nom du produit (`DeclaratifMaroc.exe`, plus `Declaration.Setup.exe`) et une icône visible (monogramme DM, pas l'icône générique — prouvé par extraction réelle).
- [x] Build `Declaration.Setup` 0 erreur (Debug **et** Release).

## Réserves (non bloquantes pour le périmètre strict de cette task)

1. **Aucune capture d'écran / pilotage réel du formulaire WinForms** dans cette session — même
   limite d'environnement que TASK-115 (pas d'accès interactif Windows/UAC). Le formulaire n'a
   donc pas été revu visuellement en conditions réelles (onglets, disposition après retrait des
   2 groupes Sage) — à confirmer par le PO sur poste réel avant clôture définitive, si jugé
   nécessaire.
2. **Réserves indépendantes de TASK-115** (preuves GUI réelles install/mise à jour/port occupé,
   décision Sage OM `Unknown`) — explicitement hors périmètre de TASK-119, inchangées, à la charge
   du PO/cycle de révision propre de TASK-115.
3. **Séquencement TASK-115 ↔ TASK-119** (signalé par la task elle-même) : ce correctif doit être
   intégré avant toute clôture définitive de TASK-115 en production, pour éviter qu'une
   installation réelle écrive des champs Sage/SageOM globaux devenus trompeurs.

## Recommandation

Build vert (Debug+Release), retrait complet et cohérent des champs Sage/SageOM (formulaire +
service + doc), icône embarquée et vérifiée par extraction réelle, renommage propagé aux 6 fichiers
identifiés **plus** un bug réel corrigé au passage (filtre d'auto-exclusion `DeploymentCopier`,
qui aurait sinon copié le nouvel exe sur lui-même). Seule réserve : pas de preuve visuelle GUI dans
cet environnement (réserve n°1), cohérente avec la limite déjà documentée et acceptée sur TASK-115.
