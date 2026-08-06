$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'P_SOCIETE' AND (COLUMN_NAME LIKE '%Nom%' OR COLUMN_NAME LIKE '%Raison%' OR COLUMN_NAME LIKE '%Libelle%' OR COLUMN_NAME LIKE '%Code%' OR COLUMN_NAME LIKE '%Id%')"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Col: $($reader.GetValue(0))"
}
$reader.Close()
$conn.Close()
