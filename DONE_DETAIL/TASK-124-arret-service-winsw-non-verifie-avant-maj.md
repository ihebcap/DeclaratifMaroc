# TASK-124 — `Stop()` WinSW ne garantit pas la libération des fichiers avant mise à jour

## Contexte
Signalement PO (19/07/2026), lors des essais de redéploiement liés à TASK-123 (rétrogradation
`net8.0`) : une mise à jour du service installé nécessite parfois un arrêt manuel du service avant
de relancer le setup, alors que `RunInstallOrUpdate` (`Declaration.Setup/SetupForm.cs:658-707`)
appelle déjà `winSw.Stop()` (ligne 674) avant `RetryOnFileLock(() => winSw.PrepareServiceFiles(...))`
(ligne 691) — l'arrêt automatique existe mais n'est, en pratique, pas toujours suffisant pour
libérer les fichiers de `Declaration.API.exe` à temps.

## Cause racine
`WinSwServiceManager.Stop()` (`Declaration.Setup/Services/WinSwServiceManager.cs:75-80`) délègue à
`RunWinSw("stop")` (lignes 94-122), qui lance `WinSW.exe stop` en sous-processus et attend
uniquement `process.WaitForExit()` (ligne 120) sur **ce processus CLI éphémère**. Or `WinSW.exe
stop` ne fait que transmettre la demande d'arrêt au SCM (`ControlService`) puis se termine
immédiatement — il ne vérifie jamais que le processus réel du service (`Declaration.API.exe`,
piloté par l'**autre** instance de WinSW qui tourne en tant que service Windows) a effectivement
terminé et libéré ses handles de fichiers (assemblies chargées, logs ouverts, etc.).

`RetryOnFileLock` (`SetupForm.cs:709-733`) est une rustine qui ré-essaie pendant 15 s en cas
d'`IOException`/`UnauthorizedAccessException`, en supposant que ce délai suffit toujours à couvrir
l'arrêt réel du service. Ce délai est variable selon la charge disque, l'antivirus, et la durée de
l'arrêt gracieux ASP.NET Core (drain des requêtes en cours, etc.) — insuffisant de façon
intermittente en pratique, d'où le besoin constaté d'arrêter le service à la main avant de relancer
le setup.

## Objectif
```
Entrée  : Stop() renvoie dès que le CLI WinSW.exe se termine, sans attendre l'arrêt réel du
          processus de service — RetryOnFileLock (15 s fixes) est le seul filet de sécurité
Traitement : Stop() attend activement la disparition réelle du processus/service avant de rendre
             la main, avec un timeout généreux et un message d'échec clair en cas de dépassement
Sortie : mise à jour fiable sans intervention manuelle, RetryOnFileLock conservé en filet de
         sécurité secondaire, pas en mécanisme d'attente primaire
```

## Périmètre STRICT
- **Inclus** :
  1. `WinSwServiceManager.Stop()` (`Declaration.Setup/Services/WinSwServiceManager.cs:75-80`) :
     après l'appel `RunWinSw("stop")`, attendre activement la fin réelle du service — via
     `ServiceController.WaitForStatus(ServiceControllerStatus.Stopped, timeout)` (SCM,
     `System.ServiceProcess`, approche préférée car directement liée à l'état du service plutôt
     qu'à un nom de process pouvant prêter à confusion) **ou**, à défaut,
     `Process.GetProcessesByName("Declaration.API")` en boucle avec poll — choix technique à
     trancher en étape 1 ci-dessous, pas figé ici.
  2. Timeout généreux et configurable en constante (ex. 30-60 s, à calibrer par essai réel, pas une
     valeur arbitraire non testée) — dépassement → exception explicite avec message clair
     (« le service ne s'est pas arrêté dans le délai imparti », pas un échec silencieux ni une
     poursuite optimiste du flux).
  3. Conserver `RetryOnFileLock` (`SetupForm.cs:718-733`) tel quel en filet de sécurité résiduel
     (fichiers verrouillés par un tiers autre que le service, antivirus, etc.) — ne plus être le
     seul mécanisme d'attente du flux d'arrêt.
- **Exclus / hors périmètre** :
  - Toute autre méthode de `WinSwServiceManager` (`Install`, `Start`, `Uninstall`,
    `PrepareServiceFiles`) — non concernées par ce défaut d'attente.
  - Le contournement d'encodage OEM (`OemEncoding`, lignes 25-32/96-100) — fonctionnalité distincte,
    déjà en place, ne pas y toucher.
  - Toute modification du gabarit WinSW XML (`DeclaratifMaroc.winsw.xml.template`) ou de la
    configuration de redémarrage sur crash — hors sujet de cette task.

## Étapes
1. Choisir le mécanisme d'attente (`ServiceController.WaitForStatus` vs poll
   `Process.GetProcessesByName`) — documenter le choix et pourquoi (ex. le SCM est la source de
   vérité de l'état du service, plus robuste qu'un nom de process qui pourrait théoriquement
   correspondre à un autre exécutable).
2. Implémenter l'attente active dans `Stop()`, avec timeout configurable et message d'erreur clair
   en cas de dépassement.
3. Test réel : installer le service, le mettre sous charge minimale (requête en cours), déclencher
   une mise à jour via le setup, confirmer qu'aucune intervention manuelle n'est nécessaire même
   sous charge — reproduire si possible la situation ayant motivé le signalement PO.
4. Test réel du cas timeout : simuler un service qui ne s'arrête pas (ex. bloquer volontairement)
   et confirmer que l'exception remonte clairement à l'utilisateur du setup, sans blocage silencieux
   ni corruption de fichiers.
5. Build `Declaration.Setup` (Debug + Release) : 0 erreur.

## Livrables
- Diff de `WinSwServiceManager.cs` (et éventuellement `SetupForm.cs` si l'appelant doit changer).
- `VERIFY/TASK-124_verify.md` : preuve réelle des deux scénarios (arrêt normal sans intervention
  manuelle sous charge, timeout avec message clair), build 0 erreur.

## Critères de validation
- Une mise à jour du service via le setup ne nécessite plus d'arrêt manuel préalable, y compris
  quand le service est sous charge au moment de la demande d'arrêt.
- En cas de dépassement du timeout, message d'erreur explicite (pas un échec silencieux, pas une
  poursuite optimiste vers `PrepareServiceFiles`).
- `RetryOnFileLock` conservé, mais plus seul mécanisme d'attente du flux d'arrêt.
- Build `Declaration.Setup` 0 erreur.

## Risques / dépendances
- **Risque de timeout mal calibré** : une valeur trop courte reproduirait le bug actuel sous une
  autre forme ; une valeur trop longue dégraderait l'expérience de mise à jour en cas de vrai
  blocage — à calibrer par essai réel, pas une valeur théorique.
- **Dépendance TASK-115** (`IN_PROGRESS/TASK-115-setup-gui-winsw-port-parametrable.md`) : ce fichier
  est au cœur du chantier TASK-115 actuellement en cours — coordination requise avec ce chantier
  avant de merger, pour éviter un conflit d'édition sur le même fichier.
- **Hors périmètre de cette task, mais lié** : `Microsoft.Extensions.Hosting.WindowsServices`/
  `Microsoft.Extensions.Configuration.Abstractions` toujours épinglés en `10.0.9` malgré le TFM
  `net8.0` (cf. rejet `VERIFY/TASK-123_verify.md`) — cause distincte d'échec de démarrage
  (`FileNotFoundException Microsoft.Extensions.Validation`), à traiter dans TASK-123, pas ici.
