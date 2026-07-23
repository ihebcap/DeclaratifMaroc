# TASK-157 — WinSW en version pré-release : l'arrêt du service laisse des process orphelins (`Declaration.API.exe` + workers OM)

## Contexte
Signalement client (23/07/2026, ~14:16) : tentative d'arrêt du service Windows **DeclaratifMaroc** en
échec, log WinSW :
```
System.InvalidOperationException: Process was not started by this object, so requested information cannot be determined.
   at System.Diagnostics.Process.EnsureState(State)
   at System.Diagnostics.Process.get_ExitCode()
   at WinSW.Util.ProcessExtensions.StopPrivate(Process, Int32)
   at WinSW.Util.ProcessExtensions.StopTree(Process, Int32)
   at WinSW.Util.ProcessExtensions.StopDescendants(Process, Int32)
   at WinSW.WrapperService.DoStop()
   at WinSW.WrapperService.OnStop()
```
Conséquence observée : `DeclaratifMaroc.exe` (le wrapper WinSW lui-même) et/ou `Declaration.API.exe`
restent actifs en arrière-plan après la demande d'arrêt, nécessitant une terminaison manuelle via le
Gestionnaire des tâches.

**Cause racine** : l'exception vient du code interne de **WinSW** (tiers, licence MIT), pas de notre
application — sa logique `StopTree`/`StopDescendants` tente d'énumérer et d'arrêter l'arbre complet des
process descendants du service (`Declaration.API.exe` et tout ce qu'il a lui-même lancé), et plante en
appelant `Process.ExitCode` sur un objet `Process` obtenu autrement que par `Process.Start()` (limitation
connue de l'API .NET sur les process découverts par PID). La version vendorisée est une **pré-release**
([Declaration.Setup/WinSW/Get-WinSW.ps1:18](Declaration.Setup/WinSW/Get-WinSW.ps1#L18) :
`v3.0.0-alpha.11`), jamais une version stable — terrain probable pour ce type de bug non stabilisé.

**Lien avec TASK-156 (DONE)** : le service lance des process enfants (`SageTaxReader.Console.exe`, un par
lecture OM, via `Process.Start` dans `WorkerInvoker.cs`) — c'est la présence d'un tel enfant vivant au
moment de la demande d'arrêt qui expose la fenêtre du bug. TASK-156 réduit la fréquence/durée des lectures
OM concurrentes, ce qui réduit la probabilité de tomber sur cette fenêtre, mais **ne l'élimine pas** : un
arrêt demandé pendant une lecture OM individuelle normale (même hors contention) peut toujours déclencher
ce plantage. C'est un défaut distinct, dans un composant tiers, pas une régression de TASK-156.

## Objectif
```
Entrée  : l'arrêt du service échoue silencieusement côté WinSW dès qu'un process enfant OM est vivant,
          laissant Declaration.API.exe (et ses propres enfants) orphelins, nécessitant une intervention
          manuelle (Gestionnaire des tâches) à chaque occurrence
Traitement : remplacer la version pré-release de WinSW par une version stable ne présentant pas ce défaut
             (ou, à défaut, contourner sa gestion de l'arbre de descendants — notre propre code
             (WorkerInvoker.Kill(), déjà en place) gère déjà la terminaison de ses enfants directs)
Sortie : l'arrêt du service se termine proprement, sans exception WinSW, sans process orphelin, même
         lorsqu'un worker OM est actif au moment de la demande d'arrêt
```

## Périmètre STRICT
- **Inclus** :
  1. `Declaration.Setup/WinSW/Get-WinSW.ps1` — version par défaut (`-Version`) à mettre à jour vers une
     **version stable** (non alpha/beta/rc) de WinSW.
  2. `Declaration.Setup/WinSW/DeclaratifMaroc.winsw.xml.template` — uniquement si la version stable retenue
     nécessite un ajustement de configuration (ex. une option pour ne pas gérer l'arbre de descendants, si
     elle existe dans cette version) ; sinon, ne pas y toucher.
  3. Régénération du binaire `WinSW.exe` vendorisé via le script (jamais committé en dur, cf. note du
     script) — le nouveau binaire remplace l'ancien dans le flux de packaging.
  4. Vérification de non-régression sur `WinSwServiceManager.cs` (`Install`/`Start`/`Stop`/`Uninstall`,
     `PrepareServiceFiles`, `WaitForApiProcessExit` de TASK-124) avec la nouvelle version — **sans modifier
     ce fichier** sauf si le comportement de sortie de `WinSW.exe stop`/`status` a changé de façon
     incompatible (à documenter précisément si c'est le cas, ne pas modifier « pour faire pareil »).
- **Exclus / hors périmètre** :
  - `Declaration.Orchestration/WorkerInvoker.cs` (gestion actuelle des process enfants applicatifs,
    `process.Kill()` sur timeout — déjà correcte, ne pas y toucher).
  - Toute logique de TASK-156 (verrou de contention, cache) — chantier distinct et déjà terminé.
  - Le renommage produit/service (`DeclaratifMaroc`) ou le wizard `Declaration.Setup` au-delà du strict
    changement de version WinSW.

## Étapes
1. **Identifier la dernière version STABLE de WinSW** (jamais une pré-release) sur
   `https://github.com/winsw/winsw/releases` au moment de l'implémentation — ne pas réutiliser un numéro
   de version deviné a priori, la liste des releases doit être consultée à ce moment précis.
2. Vérifier dans le changelog/les issues connues de cette version stable si le défaut `StopDescendants`/
   `Process was not started by this object` est documenté comme corrigé, ou au moins absent des retours
   connus — documenter la conclusion (corrigé confirmé / absence de mention / risque résiduel assumé).
3. Si la version stable retenue ne garantit pas l'absence du défaut : chercher une option de configuration
   XML pour désactiver la gestion de l'arbre de descendants par WinSW (ne laisser WinSW arrêter QUE le
   process principal `Declaration.API.exe`, jamais tenter de descendre dans son arbre de process — notre
   propre code tue déjà ses enfants directs via `WorkerInvoker.Kill()` sur timeout, donc ce n'est pas une
   perte de robustesse réelle). Documenter si une telle option existe ou non dans la version choisie.
4. Mettre à jour `Get-WinSW.ps1` (`-Version` par défaut) et régénérer `WinSW.exe`.
5. **Test réel obligatoire** (le défaut n'est reproductible qu'avec un enfant OM vivant) : installer le
   service avec la nouvelle version, déclencher une lecture OM (batch ou individuelle) pour avoir un
   `SageTaxReader.Console.exe` actif, puis demander l'arrêt du service **pendant que ce process enfant est
   vivant** — confirmer l'absence de l'exception `InvalidOperationException` dans les logs WinSW, et
   l'absence de tout process orphelin (`Declaration.API.exe` et `SageTaxReader.Console.exe`) après l'arrêt.
6. Revérifier `WaitForApiProcessExit` (TASK-124) avec la nouvelle version — confirmer qu'une mise à jour du
   service via le setup ne nécessite toujours pas d'intervention manuelle.
7. Revalider l'installation complète (`Install`/`Start`/`Stop`/`Uninstall`), pas seulement le scénario de
   ce signalement — un changement de version WinSW peut affecter d'autres comportements (format XML,
   politique `onfailure`/`restart`, encodage OEM déjà contourné dans `WinSwServiceManager.cs:25-32`).
8. Build `Declaration.Setup` (Debug + Release) : 0 erreur.

## Livrables
- `Get-WinSW.ps1` mis à jour (nouvelle version par défaut) + binaire `WinSW.exe` régénéré (non committé,
  cf. note licence MIT déjà présente dans le script).
- Éventuel ajustement de `DeclaratifMaroc.winsw.xml.template` si une option de configuration est retenue à
  l'étape 3, avec justification explicite.
- `VERIFY/TASK-157_verify.md` : preuve réelle du test de l'étape 5 (log avant/après, absence de process
  orphelin), résultat de la revalidation complète Install/Start/Stop/Uninstall, build 0 erreur.

## Critères de validation
- Demander l'arrêt du service **alors qu'un `SageTaxReader.Console.exe` est actif** ne produit plus
  l'exception `InvalidOperationException` dans les logs WinSW.
- Après l'arrêt, aucun process orphelin (`Declaration.API.exe` ni `SageTaxReader.Console.exe`) ne subsiste
  — vérifié dans le Gestionnaire des tâches, pas seulement via l'état affiché par `services.msc`.
- Le comportement de TASK-124 (`WaitForApiProcessExit`, mise à jour sans intervention manuelle) reste
  fonctionnel avec la nouvelle version de WinSW.
- Install/Start/Stop/Uninstall revalidés sans régression.
- Build `Declaration.Setup` (Debug + Release) : 0 erreur.

## Risques / dépendances
- **Aucune garantie a priori** qu'une version stable élimine totalement le défaut si sa cause est plus
  profonde qu'un simple bug de version (limitation générique de l'API `Process` de .NET sur les process
  découverts par PID plutôt que démarrés par l'appelant) — dans ce cas, le repli de l'étape 3 (désactiver la
  gestion de l'arbre de descendants côté WinSW) devient la mitigation principale, pas un simple filet.
- **Changer de version WinSW peut casser autre chose** : format du fichier XML de configuration, politique
  de redémarrage sur crash (`<onfailure>`), décodage de la sortie console (`OemEncoding`,
  `WinSwServiceManager.cs:25-32`, déjà un contournement pointu d'un bug d'encodage constaté en essai réel le
  19/07/2026) — d'où l'étape 7 (revalidation complète), pas seulement le scénario de ce signalement.
- **Dépendance douce sur TASK-115** (`IN_PROGRESS/TASK-115-setup-gui-winsw-port-parametrable.md`, wizard
  setup) : si ce fichier est encore en cours d'édition par ce chantier, coordonner pour éviter un conflit.
- **Non bloquant** : le contournement manuel (tuer les process orphelins via le Gestionnaire des tâches)
  reste disponible en attendant cette task — aucune urgence à traiter avant la prochaine fenêtre de
  maintenance côté client.
