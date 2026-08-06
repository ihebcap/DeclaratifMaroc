$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT MV_Type FROM RT_MOUVEMENT WHERE MV_Numero = 'RF26010009'"
$type = $cmd.ExecuteScalar()
Write-Host "MV_Type: $type"
$conn.Close()
