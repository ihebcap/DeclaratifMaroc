$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()

Write-Host "=== Distinct SO_Id in RT_MOUVEMENT ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT DISTINCT SO_Id FROM RT_MOUVEMENT"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  RT_MOUVEMENT SO_Id: '$($reader.GetValue(0))'"
}
$reader.Close()

Write-Host "=== Distinct SO_Id in RT_RAPPROCHEMENT ==="
$cmd.CommandText = "SELECT DISTINCT SO_Id FROM RT_RAPPROCHEMENT"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  RT_RAPPROCHEMENT SO_Id: '$($reader.GetValue(0))'"
}
$reader.Close()

$conn.Close()
