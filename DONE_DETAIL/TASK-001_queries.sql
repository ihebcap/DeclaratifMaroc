USE GR_EMA_DISTRIBUTION;

-- ==============================================================================
-- 1. Cadrage Société
-- Vérification que la base est mono-société et correspond à la base Sage
-- ==============================================================================
SELECT SO_Id, SO_RaisonSocial 
FROM P_SOCIETE;
-- Résultat attendu : SO_Id = 1, RaisonSocial = NEW_EMA DISTRIBUTION

-- ==============================================================================
-- 2. Taux de correspondance de la clé de jointure (RT_HISTCOMPTA <-> Sage)
-- ==============================================================================
SELECT 
    COUNT(*) as Total_HistCompta,
    SUM(CASE WHEN f.cbMarq IS NOT NULL THEN 1 ELSE 0 END) as Matches_Sage,
    SUM(CASE WHEN f.cbMarq IS NULL AND h.HC_No = 0 THEN 1 ELSE 0 END) as Non_Comptabilise_HC0,
    SUM(CASE WHEN f.cbMarq IS NULL AND h.HC_No <> 0 THEN 1 ELSE 0 END) as Orphelins_Reels,
    CONVERT(DECIMAL(5,2), SUM(CASE WHEN f.cbMarq IS NOT NULL THEN 1 ELSE 0 END) * 100.0 / COUNT(*)) as Taux_Correspondance_Pct
FROM RT_HISTCOMPTA h
LEFT JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq;

-- ==============================================================================
-- 3. Contrôle de Recopie Image (RT_HISTCOMPTA vs Sage)
-- ==============================================================================
SELECT 
    h.HC_No, 
    h.HC_PieceTreso as Image_PieceTreso, 
    f.EC_TresoPiece as Sage_PieceTreso, 
    h.HC_DateRappro as Image_DateRappro, 
    f.EC_DateRappro as Sage_DateRappro
FROM RT_HISTCOMPTA h 
INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq 
WHERE ISNULL(h.HC_PieceTreso, '') <> ISNULL(f.EC_TresoPiece, '') 
   OR CAST(h.HC_DateRappro AS DATE) <> CAST(f.EC_DateRappro AS DATE);
-- Résultat : 1 seul écart (HC_No = 7443) -> montre les limites de la recopie image.

-- ==============================================================================
-- 4. Cohérence Source Locale (MV_Point) vs Sage (TresoPiece)
-- Périmètre : SO_Id = 1, MV_Domaine = 1 (ReglementFournisseur), Affectations existantes
-- ==============================================================================
WITH LocalRapproches AS (
    SELECT DISTINCT m.MV_Id, m.MV_Numero, m.MV_PointDate, m.MV_Montant
    FROM RT_MOUVEMENT m
    WHERE m.SO_Id = 1 
      AND m.MV_Domaine = 1 -- 1 = ReglementFournisseur
      AND m.MV_Point = 1
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
),
SageRapproches AS (
    SELECT DISTINCT m.MV_Id, m.MV_Numero, MAX(f.EC_DateRappro) AS EC_DateRappro, m.MV_Montant
    FROM RT_MOUVEMENT m
    INNER JOIN RT_HISTCOMPTA h ON m.MV_Id = h.MV_Id
    INNER JOIN [NEW_EMA DISTRIBUTION].dbo.F_ECRITUREC f ON h.HC_No = f.cbMarq
    WHERE m.SO_Id = 1 
      AND m.MV_Domaine = 1 -- 1 = ReglementFournisseur
      AND ISNULL(f.EC_TresoPiece, '') <> ''
      AND EXISTS (SELECT 1 FROM RT_AFFECTATION a WHERE a.MV_Id = m.MV_Id)
    GROUP BY m.MV_Id, m.MV_Numero, m.MV_Montant
)
SELECT 
    (SELECT COUNT(*) FROM LocalRapproches) AS Total_Local_Rapproches,
    (SELECT COUNT(*) FROM SageRapproches) AS Total_Sage_Rapproches,
    (SELECT COUNT(*) FROM LocalRapproches l LEFT JOIN SageRapproches s ON l.MV_Id = s.MV_Id WHERE s.MV_Id IS NULL) AS Presents_Local_Absents_Sage,
    (SELECT COUNT(*) FROM SageRapproches s LEFT JOIN LocalRapproches l ON s.MV_Id = l.MV_Id WHERE l.MV_Id IS NULL) AS Presents_Sage_Absents_Local,
    (SELECT COUNT(*) FROM LocalRapproches l INNER JOIN SageRapproches s ON l.MV_Id = s.MV_Id WHERE CAST(l.MV_PointDate AS DATE) <> CAST(s.EC_DateRappro AS DATE)) AS Dates_Differentes;

-- ==============================================================================
-- 5. Réconciliation Déclarations Existantes (RT_LigneDeclarationTva vs RT_MOUVEMENT)
-- Périmètre : DTL_Domaine = 2 (Decaissement), DTL_TypePayement = 0 (Espece)
-- ==============================================================================
SELECT 
    COUNT(*) as Total_Lignes_Declarees_Decaissement,
    SUM(CASE WHEN m.MV_Point = 1 THEN 1 ELSE 0 END) as Lignes_Local_Rapproche,
    SUM(CASE WHEN m.MV_Point = 0 AND l.DTL_TypePayement = 0 THEN 1 ELSE 0 END) as Lignes_Local_NonRapproche_Espece,
    SUM(CASE WHEN m.MV_Point = 0 AND l.DTL_TypePayement <> 0 THEN 1 ELSE 0 END) as Lignes_Local_NonRapproche_Anomalies
FROM RT_LigneDeclarationTva l
INNER JOIN RT_MOUVEMENT m ON l.DTL_MvNumero = m.MV_Numero 
                         AND m.SO_Id = 1
                         AND m.MV_Domaine = 1 -- ReglementFournisseur
WHERE l.DTL_Domaine = 2; -- 2 = Decaissement
