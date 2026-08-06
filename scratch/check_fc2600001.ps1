$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT * FROM DM_LGTVA WHERE NumeroFacture = 'FC2600001'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Facture: $($reader['NumeroFacture']), Etat: $($reader['Etat']), Motif: '$($reader['MotifRejet'])', HT: $($reader['HT']), TVA: $($reader['TVA']), Rapp: '$($reader['NumeroRapprochement'])'"
}
$reader.Close()
$conn.Close()
