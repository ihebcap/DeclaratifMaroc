USE GR_EMA_DISTRIBUTION;
DECLARE @dateDebut DATE = '2026-05-01';
DECLARE @dateFin DATE = '2026-05-31';

SELECT COUNT(*) as LocalCount
FROM RT_MOUVEMENT m
WHERE m.MV_Domaine = 1
  AND m.MV_Point = 1
  AND m.MV_PointDate >= @dateDebut
  AND m.MV_PointDate < DATEADD(day, 1, @dateFin)
  AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id);

SELECT COUNT(*) as SageCount
FROM RT_MOUVEMENT m
INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
WHERE m.MV_Domaine = 1
  AND ISNULL(f.EC_TresoPiece, '') <> ''
  AND f.EC_DateRappro >= @dateDebut
  AND f.EC_DateRappro < DATEADD(day, 1, @dateFin)
  AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id);
