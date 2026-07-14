$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT NumeroFacture, Etat, MotifRejet, HT, TVA, NumeroRapprochement FROM DM_LGTVA WHERE NumeroRapprochement IN ('RF26060032','RF26060033')"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Facture: $($reader.GetValue(0)), Etat: $($reader.GetValue(1)), Motif: '$($reader.GetValue(2))', HT: $($reader.GetValue(3)), TVA: $($reader.GetValue(4)), Rapp: '$($reader.GetValue(5))'"
}
$reader.Close()
$conn.Close()
