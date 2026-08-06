$json = Get-Content -Raw -Path "$PSScriptRoot/connections.json" | ConvertFrom-Json
$connStr = $json.ConnectionStrings.GrfConnection
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "DELETE FROM DM_LGTVA; DELETE FROM DM_ENTTVA; UPDATE RT_AFFECTATION SET DT_Id = NULL;"
$cmd.ExecuteNonQuery()
$conn.Close()
