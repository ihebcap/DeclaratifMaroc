$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT MV_Date, MV_Point, MV_PointDate FROM RT_MOUVEMENT WHERE MV_Numero = 'RF26010009'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "MV_Date: $($reader.GetValue(0)), MV_Point: $($reader.GetValue(1)), MV_PointDate: $($reader.GetValue(2))"
}
$reader.Close()
$conn.Close()
