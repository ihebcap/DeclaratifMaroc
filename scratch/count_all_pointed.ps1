$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM RT_MOUVEMENT WHERE SO_Id = 1 AND MV_Domaine = 1 AND MV_Type <> 0 AND MV_Point = 1"
$count = $cmd.ExecuteScalar()
Write-Host "Total pointed movements: $count"

if ($count -gt 0) {
    $cmd.CommandText = "SELECT MIN(MV_PointDate), MAX(MV_PointDate) FROM RT_MOUVEMENT WHERE SO_Id = 1 AND MV_Domaine = 1 AND MV_Type <> 0 AND MV_Point = 1"
    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Min PointDate: $($reader.GetValue(0))"
        Write-Host "Max PointDate: $($reader.GetValue(1))"
    }
    $reader.Close()
}
$conn.Close()
