$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT SO_Id, SO_RaisonSocial FROM P_SOCIETE"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "SO_Id: $($reader.GetValue(0)) | SO_RaisonSocial: $($reader.GetValue(1))"
}
$conn.Close()
