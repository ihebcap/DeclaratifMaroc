$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT DISTINCT Etat FROM DM_LGTVA"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Etat in DM_LGTVA: $($reader.GetValue(0))"
}
$reader.Close()
$conn.Close()
