$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Etat, COUNT(*) FROM DM_LGTVA GROUP BY Etat"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Etat: $($reader.GetValue(0)) -- Count: $($reader.GetValue(1))"
}
$reader.Close()
$conn.Close()
