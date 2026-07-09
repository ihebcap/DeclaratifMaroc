USE GR_EMA_DISTRIBUTION;

WITH Rapproches AS (
    SELECT 
        m.MV_Id,
        m.MV_PointDate,
        m.MV_Point,
        CASE WHEN ISNULL(f.EC_TresoPiece, '') <> '' THEN 1 ELSE 0 END AS Sage_Point
    FROM RT_MOUVEMENT m
    INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
    INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
    WHERE m.MV_Domaine = 1 
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
)
SELECT YEAR(MV_PointDate) as Annee, MONTH(MV_PointDate) as Mois, COUNT(*) as Count
FROM Rapproches
WHERE MV_Point = 1 AND Sage_Point = 0
GROUP BY YEAR(MV_PointDate), MONTH(MV_PointDate)
ORDER BY Annee, Mois;
