# TASK-214 — Automatiser la récupération de `WinSW.exe` dans le pipeline de build (`Deploy-All.ps1`)

## Contexte
Signalement PO (12/08/2026) : au clic « Installer » du wizard `DeclaratifMaroc.exe`, échec bloquant
`FileNotFoundException` : « WinSW.exe absent — exécuter WinSW\Get-WinSW.ps1 avant de packager
DeclaratifMaroc » (`Declaration.Setup/Services/WinSwServiceManager.cs:47-58`).

Cause confirmée par lecture du pipeline (`Deploy-All.ps1` → `Declaration.Setup/deploy/Publish-Setup.ps1`,
TASK-115) : `WinSW.exe` est un binaire tiers (licence MIT, projet `winsw/winsw`) **volontairement non
committé** dans le dépôt (réserve de licence, cf. en-tête `Declaration.Setup/WinSW/Get-WinSW.ps1`). Il
doit être téléchargé une fois par `Get-WinSW.ps1` dans `Declaration.Setup\WinSW\WinSW.exe` avant que
`Publish-Setup.ps1` ne le copie dans `deploy\WinSW\`, où il est ensuite zippé (`payload.zip`) puis
embarqué en ressource dans `installer\DeclaratifMaroc.exe` (`PayloadExtractor.cs`). Aujourd'hui,
`Publish-Setup.ps1:39-45` se contente d'un `Write-Warning` non bloquant si `WinSW.exe` est absent — le
build entier (`dotnet publish`, `npm run build`, workers Sage, zip, publish Setup) se termine donc
« avec succès » apparent, et l'échec ne survient que bien plus tard, côté client, au clic « Installer ».

Demande PO explicite (12/08/2026) : « je ne fais rien manuellement » — le PO ne veut plus exécuter
`Get-WinSW.ps1` à la main avant chaque livraison ; ce doit être une étape automatique du pipeline
`Deploy-All.ps1`, pas une case à cocher mentale.

## Objectif
```
Entrée  : Get-WinSW.ps1 est un script à lancer manuellement, sans quoi l'échec n'est détecté que côté
          client (clic "Installer") via un simple Write-Warning non bloquant dans Publish-Setup.ps1.
Traitement : Deploy-All.ps1 vérifie/télécharge WinSW.exe automatiquement avant packaging, sans
             intervention manuelle ; échec de téléchargement = arrêt bloquant du pipeline (jamais un
             installer\DeclaratifMaroc.exe livré sans WinSW.exe embarqué).
Sortie  : une exécution de Deploy-All.ps1 seule (sans étape manuelle préalable) produit un
          installer\DeclaratifMaroc.exe fonctionnel au clic "Installer", sur un poste n'ayant jamais
          lancé Get-WinSW.ps1 auparavant.
```

## Périmètre STRICT
- **Inclus** :
  1. `Declaration.Setup/deploy/Publish-Setup.ps1` — remplacer le `Write-Warning` (ligne 41) par un
     appel automatique à `Declaration.Setup\WinSW\Get-WinSW.ps1` quand `WinSW.exe` est absent, puis
     revérifier sa présence : si le téléchargement échoue (pas de réseau, URL invalide, release
     renommée), lever une exception bloquante (`throw`) — jamais continuer le publish avec `WinSW.exe`
     manquant.
  2. `Deploy-All.ps1` — aucune modification de séquencement nécessaire a priori (l'appel à
     `Publish-Setup.ps1` à l'étape 4/7 suffit à couvrir le cas), sauf si l'étape 1 révèle qu'il faut
     remonter l'appel plus haut pour respecter `$ErrorActionPreference = "Stop"` correctement.
  3. Ne PAS re-télécharger si `WinSW.exe` est déjà présent et à jour (comportement idempotent déjà
     correct de `Get-WinSW.ps1` — ne pas le changer, juste l'appeler automatiquement au lieu de
     seulement avertir).
- **Exclus / hors périmètre** :
  - Changer la version de WinSW téléchargée (`-Version` par défaut de `Get-WinSW.ps1`) — sujet distinct
    de TASK-157 (pré-release actuelle, bug `StopDescendants`), ne pas mélanger les deux chantiers.
  - Vendoriser/committer `WinSW.exe` dans le dépôt — la réserve de licence MIT qui justifie le
    non-commit reste valide, cette task ne fait qu'automatiser l'appel du script existant.
  - Toute modification de `WinSwServiceManager.cs` (le message d'erreur côté client reste un filet de
    sécurité légitime si jamais un `installer\` est livré par un autre moyen que `Deploy-All.ps1`).

## Étapes
1. Modifier `Publish-Setup.ps1` : si `Test-Path $winSwExe` est faux, appeler
   `& (Join-Path $RepoRoot "Declaration.Setup\WinSW\Get-WinSW.ps1")` puis re-tester la présence du
   fichier ; si toujours absent (échec réseau/téléchargement), `throw` avec un message explicite
   (pas de warning silencieux).
2. Vérifier que l'exception levée remonte bien jusqu'à l'échec de `Deploy-All.ps1` (cohérent avec
   `$ErrorActionPreference = "Stop"` déjà en place en tête de ce script) — pas de `try/catch` qui
   avalerait l'erreur en aval.
3. **Test réel obligatoire** : sur un poste où `Declaration.Setup\WinSW\WinSW.exe` n'existe pas
   (le supprimer si présent pour le test), lancer `Deploy-All.ps1` de bout en bout **sans aucune
   étape manuelle préalable** — confirmer que `WinSW.exe` est téléchargé automatiquement, puis que
   `installer\DeclaratifMaroc.exe` généré fonctionne au clic « Installer » (service `DeclaratifMaroc`
   installé et démarré, sans l'erreur `FileNotFoundException` initiale).
4. Test négatif : simuler un échec réseau (ex. couper la connexion ou pointer temporairement une URL
   invalide) — confirmer que `Deploy-All.ps1` s'arrête avec une erreur claire, sans produire un
   `installer\DeclaratifMaroc.exe` silencieusement incomplet.

## Livrables
- `Declaration.Setup/deploy/Publish-Setup.ps1` mis à jour (téléchargement automatique + échec
  bloquant en cas d'échec réseau).
- `VERIFY/TASK-214_verify.md` : preuve réelle des étapes 3 et 4 (logs avant/après, `WinSW.exe` présent
  après un `Deploy-All.ps1` lancé sans étape manuelle préalable, installation réelle réussie au clic
  « Installer », échec bloquant reproduit et confirmé pour le test négatif).

## Critères de validation
- `Deploy-All.ps1` lancé seul, sans aucune commande manuelle préalable, sur un poste sans
  `WinSW.exe` local, produit un `installer\DeclaratifMaroc.exe` qui s'installe sans l'erreur
  `FileNotFoundException` d'origine.
- Un échec de téléchargement de `WinSW.exe` (réseau indisponible, URL invalide) arrête `Deploy-All.ps1`
  avec une erreur explicite, sans produire d'installeur incomplet.
- `Get-WinSW.ps1` n'est pas ré-exécuté inutilement quand `WinSW.exe` est déjà présent (idempotence
  conservée).
- Aucune modification de la version WinSW par défaut, aucun vendoring en dur dans le dépôt.

## Risques / dépendances
- **Dépendance réseau au moment du build** : le poste qui exécute `Deploy-All.ps1` doit avoir accès à
  `github.com` au moment du build — si ce poste est un jour isolé (build offline), cette automatisation
  devra prévoir un mode « déjà en cache, ne pas retélécharger » explicite (déjà couvert par
  l'idempotence de `Get-WinSW.ps1`, à condition que `WinSW.exe` ait déjà été téléchargé une première
  fois sur ce poste).
- **Aucun lien avec TASK-157** (version pré-release WinSW, bug `StopDescendants`) : cette task ne
  change que le déclenchement du téléchargement, jamais la version téléchargée — ne pas profiter de
  cette task pour changer `-Version` par défaut, ce serait un mélange de périmètres non demandé par
  le PO.
