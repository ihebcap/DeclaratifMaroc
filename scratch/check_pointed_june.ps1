$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM RT_MOUVEMENT WHERE SO_Id = 1 AND MV_Domaine = 1 AND MV_Type <> 0 AND MV_Point = 1 AND MV_PointDate >= '2026-06-01' AND MV_PointDate < '2026-07-01'"
$count = $cmd.ExecuteScalar()
Write-Host "Pointed movements in June 2026: $count"

$cmd.CommandText = "SELECT COUNT(*) FROM RT_MOUVEMENT WHERE SO_Id = 1 AND MV_Domaine = 1 AND MV_Type = 0 AND MV_Date >= '2026-06-01' AND MV_Date < '2026-07-01'"
$countEspeces = $cmd.ExecuteScalar()
Write-Host "Cash movements in June 2026: $countEspeces"

$conn.Close()
