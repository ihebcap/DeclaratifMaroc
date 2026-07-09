USE GR_EMA_DISTRIBUTION;

WITH LocalRapproches AS (
    SELECT DISTINCT m.MV_Id, m.MV_Numero, m.MV_PointDate, m.MV_Montant
    FROM RT_MOUVEMENT m
    WHERE m.MV_Domaine = 1
      AND m.MV_Point = 1
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
),
SageRapproches AS (
    SELECT DISTINCT m.MV_Id, m.MV_Numero, MAX(f.EC_DateRappro) AS EC_DateRappro, m.MV_Montant
    FROM RT_MOUVEMENT m
    INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
    INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
    WHERE m.MV_Domaine = 1
      AND ISNULL(f.EC_TresoPiece, '') <> ''
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
    GROUP BY m.MV_Id, m.MV_Numero, m.MV_Montant
)

SELECT 
    (SELECT COUNT(*) FROM LocalRapproches) AS Total_Local,
    (SELECT COUNT(*) FROM SageRapproches) AS Total_Sage,
    (SELECT COUNT(*) FROM LocalRapproches l LEFT JOIN SageRapproches s ON l.MV_Id = s.MV_Id WHERE s.MV_Id IS NULL) AS Presents_Local_Absents_Sage,
    (SELECT COUNT(*) FROM SageRapproches s LEFT JOIN LocalRapproches l ON s.MV_Id = l.MV_Id WHERE l.MV_Id IS NULL) AS Presents_Sage_Absents_Local,
    (SELECT COUNT(*) FROM LocalRapproches l INNER JOIN SageRapproches s ON l.MV_Id = s.MV_Id WHERE CAST(l.MV_PointDate AS DATE) <> CAST(s.EC_DateRappro AS DATE)) AS Dates_Differentes;
