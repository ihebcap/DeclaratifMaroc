$json = Get-Content -Raw -Path "$PSScriptRoot/connections.json" | ConvertFrom-Json
$connStr = $json.ConnectionStrings.GrfConnection
if ($connStr -notlike "*Connect Timeout*") {
    $connStr = $connStr + ";Connect Timeout=60;"
}

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$retry = 0
while ($true) {
    try {
        $conn.Open()
        break
    } catch {
        $retry++
        if ($retry -ge 5) { throw }
        Start-Sleep -Seconds 1
    }
}

$cmd = $conn.CreateCommand()
$cmd.CommandText = "DELETE FROM DM_LGTVA; DELETE FROM DM_ENTTVA; DELETE FROM DM_VENTILATION_SAGE_CACHE; UPDATE RT_AFFECTATION SET DT_Id = NULL;"
$cmd.ExecuteNonQuery()
$conn.Close()

