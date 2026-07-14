$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'P_SOCIETE'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "P_SOCIETE: $($reader.GetValue(0))"
}
$reader.Close()
$conn.Close()
