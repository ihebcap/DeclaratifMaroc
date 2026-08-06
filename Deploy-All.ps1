<#
.SYNOPSIS
    Pipeline complet de déploiement : build front + API + workers Sage + Declaration.Setup,
    puis empaquette le résultat dans installer\ (dossier autonome, un seul exe à lancer en admin).

.DESCRIPTION
    Fusionne le périmètre TASK-044 (build de deploy\ : front, API, workers) et TASK-115
    (DeclaratifMaroc.exe + WinSW) en une seule commande, à la demande explicite du PO
    (décision du 18/07/2026) — jusqu'ici ces étapes restaient volontairement séparées
    (cf. VERIFY/TASK-115_verify.md, DOCS/DEPLOIEMENT.md : "ne pas improviser cette fusion").
    TASK-044 reste la tâche de référence pour tout ce qui touche au build de deploy\ ; ce script
    en est une implémentation, pas un remplacement de sa documentation.

    deploy\ et installer\ ont des rôles différents (décision PO du 18/07/2026) :
      - deploy\      : dossier de build "normal", multi-fichiers (API, front, workers, WinSW,
                       DeclaratifMaroc.exe classique, ex-Declaration.Setup.exe TASK-119) — usage
                       dev/interne, comme avant.
      - installer\   : UN SEUL fichier, DeclaratifMaroc.exe, à livrer tel quel au client. Le
                       payload (API/front/workers/WinSW) est embarqué en ressource dans l'exe
                       (cf. Declaration.Setup\Services\PayloadExtractor.cs) plutôt que copié à
                       côté — extrait dans un dossier temporaire au moment de l'install/MAJ, puis
                       nettoyé.

    Étapes :
      1. dotnet publish Declaration.API                -> deploy\
      2. npm run build (front)                         -> deploy\wwwroot\
      3. publish-sagetaxreader-workers.ps1              -> deploy\workers\<version>\
      4. Declaration.Setup\deploy\Publish-Setup.ps1     -> deploy\DeclaratifMaroc.exe + deploy\WinSW\
      5. Zip du payload (deploy\ sans connections.json ni DeclaratifMaroc.exe/.pdb — exclusion par
         nom exact, PAS par wildcard "DeclaratifMaroc.*" : ce pattern collisionnait avec
         WinSW\DeclaratifMaroc.winsw.xml.template et l'excluait aussi du payload, cf. bug constaté
         en essai réel le 19/07/2026, FileNotFoundException au clic Installer)
         -> Declaration.Setup\payload.zip
      6. dotnet publish Declaration.Setup (single-file, payload.zip embarqué) -> installer\
      7. Suppression de Declaration.Setup\payload.zip (ne doit pas traîner ni être committé)

    Résultat : installer\DeclaratifMaroc.exe — le seul fichier à livrer/copier sur le poste
    client, à lancer en administrateur.

.PARAMETER SkipFront
    Ignore le build npm du front (utile si seul le back a changé).

.PARAMETER SkipWorkers
    Ignore la republication des workers Sage (build long : une variante par version Sage).
#>
param(
    [switch]$SkipFront,
    [switch]$SkipWorkers
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$DeployPath = Join-Path $RepoRoot "deploy"
$InstallerPath = Join-Path $RepoRoot "installer"

Write-Host "=== 1/5 : dotnet publish Declaration.API (self-contained win-x64) -> deploy\ ===" -ForegroundColor Cyan
# Self-contained obligatoire (pas seulement "recommandé", cf. DOCS\DEPLOIEMENT.md) : sans -r
# win-x64 --self-contained true, l'apphost `Declaration.API.exe` publié dépend du runtime partagé
# .NET installé sur le poste cible. WinSW invoque cet exe directement (WinSW\*.xml.template,
# <executable>%BASE%\Declaration.API.exe</executable>), sans passer par `dotnet` : sur un poste
# client qui n'a pas le runtime partagé requis, le service ne démarre pas — bug constaté en
# essai réel le 19/07/2026 après correction du bug Install()/SCM (cf. VERIFY/TASK-115_verify.md).
#
# TASK-123 : suppression de deploy\ AVANT publish, obligatoire — `dotnet publish` ne nettoie jamais
# un dossier de sortie existant (il ne fait qu'ajouter/écraser les fichiers du nouveau graphe de
# dépendances, sans supprimer les DLL orphelines d'un publish précédent). Constaté en essai réel :
# après la rétrogradation net10.0 -> net8.0 (changement d'asset résolu pour Microsoft.Data.SqlClient
# et suppression de la dépendance transitive Microsoft.Extensions.Validation via l'ancien
# Microsoft.AspNetCore.OpenApi 10.0.9), un publish incrémental sur deploy\ existant laissait les
# anciennes DLL net9.0/net10.0 en place (hash identique à l'ancien build, horodatage de la source
# NuGet plus ancien que le fichier déjà présent -> la copie incrémentale de MSBuild les considère
# "à jour" et ne les recopie pas) : deploy\Microsoft.Data.SqlClient.dll restait le binaire net9.0
# alors que deploy\Declaration.API.deps.json déclarait déjà net8.0, et deploy\
# Microsoft.Extensions.Validation.dll (dépendance disparue) restait présent sans être référencé par
# aucun deps.json — cause du FileNotFoundException observé lors d'un précédent essai de déploiement.
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $DeployPath
dotnet publish (Join-Path $RepoRoot "Declaration.API\Declaration.API.csproj") -c Release -o $DeployPath -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "Publish Declaration.API échoué (code $LASTEXITCODE)" }

if (-not $SkipFront) {
    Write-Host "=== 2/5 : npm run build (front) -> deploy\wwwroot\ ===" -ForegroundColor Cyan
    Push-Location (Join-Path $RepoRoot "declaration-tva-web")
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build échoué (code $LASTEXITCODE)" }
    }
    finally {
        Pop-Location
    }
    New-Item -ItemType Directory -Force (Join-Path $DeployPath "wwwroot") | Out-Null
    Copy-Item (Join-Path $RepoRoot "declaration-tva-web\dist\*") (Join-Path $DeployPath "wwwroot") -Recurse -Force
}
else {
    Write-Host "=== 2/5 : front ignoré (-SkipFront) ===" -ForegroundColor Yellow
}

if (-not $SkipWorkers) {
    Write-Host "=== 3/5 : workers Sage -> deploy\workers\<version>\ ===" -ForegroundColor Cyan
    & (Join-Path $RepoRoot "publish-sagetaxreader-workers.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Publication des workers Sage échouée (code $LASTEXITCODE)" }
}
else {
    Write-Host "=== 3/5 : workers Sage ignorés (-SkipWorkers) ===" -ForegroundColor Yellow
}

Write-Host "=== 4/7 : DeclaratifMaroc.exe (classique) + WinSW -> deploy\ ===" -ForegroundColor Cyan
& (Join-Path $RepoRoot "Declaration.Setup\deploy\Publish-Setup.ps1") -RepoRoot $RepoRoot -DeployPath $DeployPath

$SetupProject = Join-Path $RepoRoot "Declaration.Setup"
$PayloadZip = Join-Path $SetupProject "payload.zip"

Write-Host "=== 5/7 : zip du payload (deploy\ sans connections.json / DeclaratifMaroc.exe+pdb) ===" -ForegroundColor Cyan
Remove-Item -Force -ErrorAction SilentlyContinue $PayloadZip
$payloadStaging = Join-Path $RepoRoot "obj-payload-staging"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $payloadStaging
New-Item -ItemType Directory -Force $payloadStaging | Out-Null
robocopy $DeployPath $payloadStaging /MIR /XF connections.json "DeclaratifMaroc.exe" "DeclaratifMaroc.pdb" /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy deploy\ -> staging payload a échoué (code $LASTEXITCODE)" }
Compress-Archive -Path (Join-Path $payloadStaging "*") -DestinationPath $PayloadZip -CompressionLevel Optimal
Remove-Item -Recurse -Force $payloadStaging

Write-Host "=== 6/7 : dotnet publish Declaration.Setup (single-file, payload embarqué) -> installer\ ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $InstallerPath
dotnet publish (Join-Path $SetupProject "Declaration.Setup.csproj") -c Release -o $InstallerPath -r win-x64 --self-contained true -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Publish Declaration.Setup (installeur unique) échoué (code $LASTEXITCODE)" }

Write-Host "=== 7/7 : nettoyage payload.zip (ne doit pas rester dans le repo) ===" -ForegroundColor Cyan
Remove-Item -Force -ErrorAction SilentlyContinue $PayloadZip

Write-Host ""
Write-Host "Terminé. Installeur prêt : $InstallerPath\DeclaratifMaroc.exe (fichier unique)" -ForegroundColor Green
Write-Host "Lancer ce fichier EN TANT QU'ADMINISTRATEUR sur le poste cible — c'est le seul fichier à livrer au client."
