$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM RT_AFFECTATION WHERE DT_Id IS NOT NULL"
$count = $cmd.ExecuteScalar()
Write-Host "Affectations with DT_Id IS NOT NULL: $count"

$cmd.CommandText = "SELECT DISTINCT DT_Id FROM RT_AFFECTATION WHERE DT_Id IS NOT NULL"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "  DT_Id: $($reader.GetValue(0))"
}
$reader.Close()

$conn.Close()
