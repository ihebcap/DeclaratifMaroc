$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()

Write-Host "=== Columns of DM_ENTTVA ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DM_ENTTVA'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  $($reader.GetValue(0))"
}
$reader.Close()

Write-Host "=== Columns of DM_LGTVA ==="
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DM_LGTVA'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  $($reader.GetValue(0))"
}
$reader.Close()

$conn.Close()
