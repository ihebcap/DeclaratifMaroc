Add-Type -Path 'D:\_vibe\GRF\GRFWinform\Tresorerie.Core.dll'
$assemblies = [System.AppDomain]::CurrentDomain.GetAssemblies()
$tresorerieAsm = $assemblies | Where-Object { $_.ManifestModule.Name -eq 'Tresorerie.Core.dll' }
if ($tresorerieAsm) {
    $types = $tresorerieAsm.GetExportedTypes()
    $enumTypes = $types | Where-Object { $_.IsEnum -and ($_.Name -match 'Mode') }
    foreach ($t in $enumTypes) {
        Write-Host ""
        Write-Host "Enum: $($t.FullName)"
        $names = [Enum]::GetNames($t)
        foreach ($name in $names) {
            $val = [int][Enum]::Parse($t, $name)
            Write-Host "  $name = $val"
        }
    }
} else {
    Write-Host "Assembly not found!"
}
