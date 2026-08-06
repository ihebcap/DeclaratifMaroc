$conn = New-Object System.Data.SqlClient.SqlConnection("Server=.\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT E.DO_Numero, A.MV_Id, M.MV_Numero, M.MV_Point, M.MV_PointDate, M.MV_Date FROM RT_AFFECTATION A JOIN RT_MOUVEMENT M ON A.MV_Id = M.MV_Id JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id WHERE E.DO_Numero = 'FC2600001'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Facture: $($reader.GetValue(0)), MV_Id: $($reader.GetValue(1)), MV_Numero: $($reader.GetValue(2)), MV_Point: $($reader.GetValue(3)), MV_PointDate: $($reader.GetValue(4)), MV_Date: $($reader.GetValue(5))"
}
$reader.Close()
$conn.Close()
