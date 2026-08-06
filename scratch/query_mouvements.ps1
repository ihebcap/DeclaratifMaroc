$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT DISTINCT SO_Id FROM RT_MOUVEMENT"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "SO_Id in RT_MOUVEMENT: $($reader.GetValue(0))"
}
$conn.Close()
