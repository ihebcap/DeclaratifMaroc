$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()

Write-Host "=== 1. Distinct NumeroRapprochement in DM_LGTVA ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT DISTINCT TOP 5 NumeroRapprochement FROM DM_LGTVA"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  DM_LGTVA: '$($reader.GetValue(0))'"
}
$reader.Close()

Write-Host "=== 2. Distinct MV_Numero in RT_MOUVEMENT ==="
$cmd.CommandText = "SELECT DISTINCT TOP 5 MV_Numero FROM RT_MOUVEMENT"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  RT_MOUVEMENT: '$($reader.GetValue(0))'"
}
$reader.Close()

$conn.Close()
