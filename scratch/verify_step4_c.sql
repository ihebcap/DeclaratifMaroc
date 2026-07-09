USE GR_EMA_DISTRIBUTION;

DECLARE @dateDebut DATE = '20260501';
DECLARE @dateFin DATE = '20260531';

-- Local (Nouveau)
SELECT m.MV_Id, m.MV_Numero, m.MV_PointDate, m.MV_Montant
INTO #Local
FROM RT_MOUVEMENT m
WHERE m.MV_Domaine = 1
  AND m.MV_Point = 1
  AND m.MV_PointDate >= @dateDebut
  AND m.MV_PointDate < DATEADD(day, 1, @dateFin)
  AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id);

-- Sage (Ancien)
SELECT m.MV_Id, m.MV_Numero, f.EC_DateRappro, m.MV_Montant
INTO #Sage
FROM RT_MOUVEMENT m
INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
WHERE m.MV_Domaine = 1
  AND ISNULL(f.EC_TresoPiece, '') <> ''
  AND f.EC_DateRappro >= @dateDebut
  AND f.EC_DateRappro < DATEADD(day, 1, @dateFin)
  AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id);

PRINT 'Total Local (Nouveau) :';
SELECT COUNT(*) FROM #Local;

PRINT 'Total Sage (Ancien) :';
SELECT COUNT(*) FROM #Sage;

PRINT 'Presents en Local, absents de Sage (=> ajoutes dans le nouveau) :';
SELECT COUNT(*) FROM #Local l LEFT JOIN #Sage s ON l.MV_Id = s.MV_Id WHERE s.MV_Id IS NULL;

PRINT 'Presents dans Sage, absents de Local (=> supprimes dans le nouveau) :';
SELECT COUNT(*) FROM #Sage s LEFT JOIN #Local l ON s.MV_Id = l.MV_Id WHERE l.MV_Id IS NULL;

DROP TABLE #Local;
DROP TABLE #Sage;
