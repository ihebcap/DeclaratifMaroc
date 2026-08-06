Get-ChildItem -Path 'D:\_vibe\GRF\GRFWinform' -Filter '*.dll' | ForEach-Object {
    try {
        Add-Type -Path $_.FullName -ErrorAction SilentlyContinue
    } catch {}
}
$assemblies = [System.AppDomain]::CurrentDomain.GetAssemblies()
$enumTypes = $assemblies | Select-Object -ExpandProperty ExportedTypes -ErrorAction SilentlyContinue | Where-Object { $_.IsEnum }
foreach ($t in $enumTypes) {
    if ($t.Name -match 'Mode|Paiement|Reglement|Espece|Frais') {
        Write-Host ""
        Write-Host "Enum: $($t.FullName)"
        $names = [Enum]::GetNames($t)
        foreach ($name in $names) {
            $val = [int][Enum]::Parse($t, $name)
            Write-Host "  $name = $val"
        }
    }
}
