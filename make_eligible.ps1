$json = Get-Content -Raw -Path "$PSScriptRoot/connections.json" | ConvertFrom-Json
$connStr = $json.ConnectionStrings.GrfConnection
if ($connStr -notlike "*Connect Timeout*") {
    $connStr = $connStr + ";Connect Timeout=60;"
}

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$retry = 0
while ($true) {
    try {
        $conn.Open()
        break
    } catch {
        $retry++
        if ($retry -ge 5) { throw }
        Start-Sleep -Seconds 1
    }
}

$cmd = $conn.CreateCommand()

# TASK-050 / TASK-204 alignement fixture facture-first (pivot RT_ECHEANCE):
# Constantes GrfEnums:
#   SO_Id = 1
#   EcType_FactureErp = 0, EcType_Solde = 4, EcType_Fgr = 111
#   ErpDomaine_Achat = 1
#   Domaine_ReglementFournisseur = 1, Domaine_Depense = 6
#   Point_Oui = 1, Compta_Comptabilise = 1, Annule_Non = 0, Impaye_NonImpaye = 0

$cmd.CommandText = @"
UPDATE A
SET A.DT_Id = NULL
FROM RT_AFFECTATION A
JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
WHERE E.SO_Id = 1
  AND E.EC_Type IN (0, 4, 111)
  AND E.DO_Domaine IN (0, 1)
  AND E.DO_Date >= '20260601' AND E.DO_Date < '20260701';

UPDATE M
SET M.MV_Compta = 1,
    M.MV_Annule = 0,
    M.MV_Impaye = 0,
    M.MV_Point = 1,
    M.MV_PointDate = '20260615',
    M.MV_Date = '20260615',
    M.SO_Id = 1,
    M.MV_Domaine = CASE WHEN M.MV_Domaine IN (0, 1, 6) THEN M.MV_Domaine ELSE 1 END,
    M.CT_Type = CASE WHEN M.MV_Domaine = 0 THEN 0 ELSE 1 END
FROM RT_MOUVEMENT M
JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id
JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
WHERE E.SO_Id = 1
  AND E.EC_Type IN (0, 4, 111)
  AND E.DO_Domaine IN (0, 1)
  AND E.DO_Date >= '20260601' AND E.DO_Date < '20260701';

DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = 1;

INSERT INTO DM_VENTILATION_SAGE_CACHE 
(SO_Id, EC_Id, Taux, CodeTaxe, BaseHT, MontantTva, TTC, TotalHT, TotalTva, TotalTtc, Token_MV_Id, Token_MV_Point, DateLecture, Source, MotifErreur, BrutHT, BrutTva, BrutParafiscale, BrutTtc)
SELECT 
    1 AS SO_Id,
    A.EC_Id,
    20.00 AS Taux,
    'D20' AS CodeTaxe,
    ROUND(SUM(A.AF_Montant) / 1.20, 2) AS BaseHT,
    SUM(A.AF_Montant) - ROUND(SUM(A.AF_Montant) / 1.20, 2) AS MontantTva,
    SUM(A.AF_Montant) AS TTC,
    ROUND(SUM(A.AF_Montant) / 1.20, 2) AS TotalHT,
    SUM(A.AF_Montant) - ROUND(SUM(A.AF_Montant) / 1.20, 2) AS TotalTva,
    SUM(A.AF_Montant) AS TotalTtc,
    MAX(A.MV_Id) AS Token_MV_Id,
    1 AS Token_MV_Point,
    GETDATE() AS DateLecture,
    'SAGE' AS Source,
    NULL AS MotifErreur,
    ROUND(SUM(A.AF_Montant) / 1.20, 2) AS BrutHT,
    SUM(A.AF_Montant) - ROUND(SUM(A.AF_Montant) / 1.20, 2) AS BrutTva,
    0 AS BrutParafiscale,
    SUM(A.AF_Montant) AS BrutTtc
FROM RT_AFFECTATION A
JOIN RT_ECHEANCE E ON A.EC_Id = E.EC_Id
WHERE E.SO_Id = 1
  AND A.DT_Id IS NULL
GROUP BY A.EC_Id;
"@

$cmd.ExecuteNonQuery()
$conn.Close()
