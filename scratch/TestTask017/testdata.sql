USE GR_EMA_DISTRIBUTION;
GO

DECLARE @mvId INT;
DECLARE @count INT;

SELECT @count = COUNT(*) FROM RT_MOUVEMENT WHERE MV_Identifiant = 'IF_TEST_017';
IF @count = 0
BEGIN
    INSERT INTO RT_MOUVEMENT (MV_No, SO_Id, MV_Domaine, MV_Point, MV_PointDate, MV_Date, MV_DECAISSE, MV_Compta, MV_Annule, MV_Impaye, MV_Type, CT_Code, CT_Intitule, MV_Identifiant, MV_Ice, MV_OriginReglementNature, MV_ReglementNature, CRT_Id)
    VALUES (99999, 1, 2, 1, '20260115', '20260115', 1, 1, 0, 0, 1, 'F001', 'FOURNISSEUR TEST', 'IF_TEST_017', 'ICE123', 0, 0, 0);
    
    SET @mvId = SCOPE_IDENTITY();
    
    INSERT INTO RT_AFFECTATION (AF_No, AF_Date, AF_Montant, MV_Id, EC_Id, DT_Id)
    VALUES (999, '20260115', 1000, @mvId, 22229, NULL);
    
    PRINT 'Inserted.';
END
ELSE
BEGIN
    UPDATE RT_AFFECTATION SET DT_Id = NULL WHERE MV_Id IN (SELECT MV_Id FROM RT_MOUVEMENT WHERE MV_Identifiant = 'IF_TEST_017');
    PRINT 'Updated.';
END
GO
