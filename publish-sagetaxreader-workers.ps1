# Publie une variante de SageTaxReader.Console par version Sage supportée (TASK-101).
# Chaque variante référence son propre Interop.Objets100cLib.dll (SageTaxReader\libs\<version>\,
# via la propriété MSBuild SageInteropVersion) et produit un exécutable distinct dans
# deploy\workers\<version>\ — un déploiement client ne copie que le dossier de SA version.
#
# v10 sert aussi les clients Sage v11 (DLL interop confirmés bit-à-bit identiques, cf. TASK-101).
# v8 n'a pas de variante : aucun DLL interop v8 fourni par le PO à ce jour.
#
# Usage : depuis la racine du repo -> .\publish-sagetaxreader-workers.ps1

$ErrorActionPreference = "Stop"

$versions = @("v7", "v9", "v10", "v12")
$consoleProj = "SageTaxReader\SageTaxReader.Console\SageTaxReader.Console.csproj"

foreach ($v in $versions) {
    Write-Host "=== Publish variante Sage $v ===" -ForegroundColor Cyan

    # Nettoyage complet obj/bin avant chaque variante : EmbedInteropTypes fige les types Sage
    # dans SageTaxReader.Core.dll au moment du build — réutiliser un obj/bin d'une autre version
    # mélangerait des types interop incompatibles (constaté en investigation TASK-101).
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue `
        "SageTaxReader\SageTaxReader.Core\obj", "SageTaxReader\SageTaxReader.Core\bin", `
        "SageTaxReader\SageTaxReader.Console\obj", "SageTaxReader\SageTaxReader.Console\bin", `
        "SageTaxReader\SageTaxReader.Contracts\obj", "SageTaxReader\SageTaxReader.Contracts\bin"

    $outDir = "deploy\workers\$v"
    dotnet publish $consoleProj -c Release -o $outDir -p:SageInteropVersion=$v
    if ($LASTEXITCODE -ne 0) {
        throw "Publish échoué pour la variante Sage $v"
    }
}

Write-Host ""
Write-Host "Variantes publiées dans deploy\workers\<version>\ :" -ForegroundColor Green
Write-Host "  v7   -> Sage 100 v7"
Write-Host "  v9   -> Sage 100 v9"
Write-Host "  v10  -> Sage 100 v10 ET v11 (DLL interop identiques, cf. TASK-101)"
Write-Host "  v12  -> Sage 100 v12 (référence historique par défaut)"
Write-Host ""
Write-Host "Pour chaque client : copier deploy\workers\<version>\ à côté de connections.json"
Write-Host "et faire pointer WorkerConfig.WorkerExePath vers le SageTaxReader.Console*.exe de ce dossier."
