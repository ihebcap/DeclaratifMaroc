$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Exercice, Periode, SocieteId FROM DM_ENTTVA"
$reader = $cmd.ExecuteReader()
Write-Host "=== Declarations in DM_ENTTVA ==="
while ($reader.Read()) {
    Write-Host "  DT_Id: '$($reader.GetValue(0))', Exercice: $($reader.GetValue(1)), Periode: $($reader.GetValue(2)), SO_Id: $($reader.GetValue(3))"
}
$reader.Close()
$conn.Close()
