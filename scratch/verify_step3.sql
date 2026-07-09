USE GR_EMA_DISTRIBUTION;

WITH Rapproches AS (
    SELECT 
        m.MV_Id,
        m.MV_Numero,
        m.MV_Point,
        m.MV_PointDate,
        CASE WHEN ISNULL(f.EC_TresoPiece, '') <> '' THEN 1 ELSE 0 END AS Sage_Point,
        f.EC_DateRappro AS Sage_PointDate,
        h.HC_PieceTreso AS Hist_PieceTreso,
        h.HC_DateRappro AS Hist_DateRappro
    FROM RT_MOUVEMENT m
    INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
    INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
    WHERE m.MV_Domaine = 1 
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
)
SELECT * INTO #TempRapproches FROM Rapproches;

PRINT 'Total reglements fournisseurs affectes envoyes en compta:';
SELECT COUNT(*) FROM #TempRapproches;

PRINT '---------------------------------------------------';
PRINT 'Divergence 1 : Rapproche en Local mais PAS dans Sage';
SELECT * FROM #TempRapproches 
WHERE MV_Point = 1 AND Sage_Point = 0;

PRINT '---------------------------------------------------';
PRINT 'Divergence 2 : Rapproche dans Sage mais PAS en Local';
SELECT * FROM #TempRapproches 
WHERE MV_Point = 0 AND Sage_Point = 1;

PRINT '---------------------------------------------------';
PRINT 'Divergence 3 : Dates de rapprochement differentes';
SELECT * FROM #TempRapproches 
WHERE MV_Point = 1 AND Sage_Point = 1 
  AND CAST(MV_PointDate AS DATE) <> CAST(Sage_PointDate AS DATE);

PRINT '---------------------------------------------------';
PRINT 'Controle Image (RT_HISTCOMPTA) vs Sage (doit etre vide si synchro exacte)';
SELECT * FROM #TempRapproches
WHERE (CASE WHEN ISNULL(Hist_PieceTreso, '') <> '' THEN 1 ELSE 0 END) <> Sage_Point
   OR (Sage_Point = 1 AND CAST(Hist_DateRappro AS DATE) <> CAST(Sage_PointDate AS DATE));

DROP TABLE #TempRapproches;
