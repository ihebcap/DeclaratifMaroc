$json = Get-Content -Raw -Path "$PSScriptRoot/connections.json" | ConvertFrom-Json
$connStr = $json.ConnectionStrings.GrfConnection
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "UPDATE RT_AFFECTATION SET DT_Id = NULL WHERE AF_Id IN (SELECT TOP 10 A.AF_Id FROM RT_MOUVEMENT M JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id WHERE M.MV_Point = 1 AND M.MV_Domaine IN (1,3) AND M.MV_DECAISSE = 1); UPDATE RT_MOUVEMENT SET MV_Compta = 1, MV_Annule = 0, MV_Impaye = 0, MV_PointDate = '20260615' WHERE MV_Id IN (SELECT TOP 10 M.MV_Id FROM RT_MOUVEMENT M JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id WHERE M.MV_Point = 1 AND M.MV_Domaine IN (1,3) AND M.MV_DECAISSE = 1);"
$cmd.ExecuteNonQuery()
$conn.Close()
