$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT DT_Id, SO_Id, DT_DateDebut FROM RT_DeclarationTva"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "DT_Id: $($reader.GetValue(0)) | SO_Id: $($reader.GetValue(1)) | DateDebut: $($reader.GetValue(2))"
}
$conn.Close()
