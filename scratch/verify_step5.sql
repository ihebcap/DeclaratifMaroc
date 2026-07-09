USE GR_EMA_DISTRIBUTION;

SELECT 
    COUNT(*) as Total_Lignes_Declarees_Decaissement,
    SUM(CASE WHEN m.MV_Point = 1 THEN 1 ELSE 0 END) as Lignes_Local_Rapproche,
    SUM(CASE WHEN m.MV_Point = 0 THEN 1 ELSE 0 END) as Lignes_Local_NonRapproche
FROM RT_LigneDeclarationTva l
INNER JOIN RT_MOUVEMENT m ON l.DTL_MvNumero = m.MV_Numero AND m.MV_Domaine = 1
WHERE l.DTL_Domaine = 2; -- Decaissement
