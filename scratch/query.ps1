$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT * FROM DM_ENTTVA"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Id: $($reader.GetValue(0)) | Numero: $($reader.GetValue(1)) | SocieteId: $($reader.GetValue(2)) | Exercice: $($reader.GetValue(3))"
}
$conn.Close()
