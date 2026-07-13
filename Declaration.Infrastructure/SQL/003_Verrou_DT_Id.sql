-- =====================================================================
-- TASK-064 — Trigger : immuabilité TOTALE d'une affectation déclarée
-- Cible : base GRF (GR_EMA_DISTRIBUTION) — SQL Server T-SQL
-- À exécuter UNE FOIS par l'administrateur de base.
-- =====================================================================
-- Options SET : on force QUOTED_IDENTIFIER/ANSI_NULLS ON pour que la création
-- d'objets réussisse quel que soit le client (ex. sqlcmd, dont le défaut est OFF).
-- NB : l'index créé plus bas est volontairement NON filtré (voir §0) afin de ne
-- PAS imposer QUOTED_IDENTIFIER ON aux writers legacy GRFN/Sage de RT_AFFECTATION.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- =====================================================================
-- Hypothèses confirmées sur GR_EMA_DISTRIBUTION :
--   RT_AFFECTATION.DT_Id   INT NULL (FK → table déclaration legacy, peut rester orpheline)
--   RT_MOUVEMENT.MV_Point  INT (1 = pointé, GrfEnums.Point_Oui = 1)
--   RT_MOUVEMENT.MV_Compta INT (1 = comptabilisé, GrfEnums.Compta_Comptabilise = 1)
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 0. Index utile pour les triggers RT_MOUVEMENT (EXISTS par MV_Id)
--    IMPORTANT : index NON filtré volontairement. Un index filtré
--    (WHERE DT_Id IS NOT NULL) impose SET QUOTED_IDENTIFIER ON à TOUT
--    writer de RT_AFFECTATION (msg 1934) — or GRFN/Sage legacy peuvent
--    écrire avec QUOTED_IDENTIFIER OFF, ce qui casserait leurs flux
--    légitimes. Un index ordinaire (MV_Id, DT_Id) sert le seek du trigger
--    sans imposer d'option de session aux applis existantes.
-- ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_RT_AFFECTATION_MV_Id_DT_Id'
      AND object_id = OBJECT_ID('dbo.RT_AFFECTATION')
)
BEGIN
    CREATE INDEX IX_RT_AFFECTATION_MV_Id_DT_Id
        ON dbo.RT_AFFECTATION (MV_Id, DT_Id);
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 1. Trigger immuabilité RT_AFFECTATION
--    - Interdit DELETE d'une affectation DT_Id IS NOT NULL.
--    - Interdit TOUTE modification d'une affectation déclarée (DT_Id NOT NULL).
--    - AUTORISE UNIQUEMENT la transition de dé-tamponnage PUR (DT_Id : valeur → NULL),
--      si toutes les autres colonnes protégées restent inchangées.
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TR_RT_AFFECTATION_Immuabilite', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RT_AFFECTATION_Immuabilite;
GO

CREATE TRIGGER dbo.TR_RT_AFFECTATION_Immuabilite
ON dbo.RT_AFFECTATION
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1a. Bloquer DELETE d'une affectation déclarée ──────────────────
    IF EXISTS (
        SELECT 1 FROM deleted d
        WHERE d.DT_Id IS NOT NULL
          -- La ligne supprimée est une suppression réelle (pas UPDATE)
          AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.AF_Id = d.AF_Id)
    )
    BEGIN
        DECLARE @dtIdDel INT;
        SELECT TOP 1 @dtIdDel = d.DT_Id FROM deleted d
        WHERE d.DT_Id IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.AF_Id = d.AF_Id);

        THROW 50028, N'Opération interdite — cette affectation est incluse dans la déclaration TVA et ne peut pas être supprimée. Rouvrez la déclaration avant toute modification.', 1;
    END;

    -- ── 1b. Bloquer tout UPDATE si DT_Id NOT NULL ──────────────────────
    -- On interdit toute modification si la ligne ÉTAIT déjà déclarée (d.DT_Id IS NOT NULL).
    -- La seule exception autorisée est le dé-tamponnage PUR (transition DT_Id : valeur → NULL)
    -- où absolument aucune autre colonne protégée n'a été modifiée.
    -- Les colonnes protégées excluent DT_Id et la colonne technique RowVersion.
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON i.AF_Id = d.AF_Id
        WHERE d.DT_Id IS NOT NULL          -- était déclarée avant
          AND (
              i.DT_Id IS NOT NULL          -- reste déclarée après (pas un dé-tamponnage)
              OR (                         -- dé-tamponnage non pur
                  i.DT_Id IS NULL
                  AND EXISTS (
                      SELECT i.AF_Montant, i.MV_Id, i.EC_Id, i.AF_Date, i.AF_No, i.AF_MtDevise, i.AF_EcId, i.AF_NbrJourReg, i.AF_DelaiMoyen, i.AF_IsSynchro, i.AF_IsImporterFromErp
                      EXCEPT
                      SELECT d.AF_Montant, d.MV_Id, d.EC_Id, d.AF_Date, d.AF_No, d.AF_MtDevise, d.AF_EcId, d.AF_NbrJourReg, d.AF_DelaiMoyen, d.AF_IsSynchro, d.AF_IsImporterFromErp
                  )
              )
          )
    )
    BEGIN
        DECLARE @dtIdUpd INT;
        SELECT TOP 1 @dtIdUpd = d.DT_Id
        FROM inserted i
        JOIN deleted d ON i.AF_Id = d.AF_Id
        WHERE d.DT_Id IS NOT NULL
          AND (
              i.DT_Id IS NOT NULL
              OR (
                  i.DT_Id IS NULL
                  AND EXISTS (
                      SELECT i.AF_Montant, i.MV_Id, i.EC_Id, i.AF_Date, i.AF_No, i.AF_MtDevise, i.AF_EcId, i.AF_NbrJourReg, i.AF_DelaiMoyen, i.AF_IsSynchro, i.AF_IsImporterFromErp
                      EXCEPT
                      SELECT d.AF_Montant, d.MV_Id, d.EC_Id, d.AF_Date, d.AF_No, d.AF_MtDevise, d.AF_EcId, d.AF_NbrJourReg, d.AF_DelaiMoyen, d.AF_IsSynchro, d.AF_IsImporterFromErp
                  )
              )
          );

        DECLARE @msgAfUpd NVARCHAR(500) =
            N'Opération interdite — affectation incluse dans la déclaration TVA n° '
            + CAST(@dtIdUpd AS NVARCHAR(20))
            + N' — toute modification est refusée. Rouvrez la déclaration avant correction.';
        THROW 50028, @msgAfUpd, 1;
    END;
END;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 2. Trigger immuabilité RT_MOUVEMENT
--    - Si le mouvement possède ≥1 affectation DT_Id IS NOT NULL :
--        • Interdit MV_Point 1 → 0 (dérapprochement)
--        • Interdit MV_Compta 1 → 0 (décomptabilisation)
--    - Tout le reste passe (mise à jour neutre, tamponnage…).
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TR_RT_MOUVEMENT_Immuabilite', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RT_MOUVEMENT_Immuabilite;
GO

CREATE TRIGGER dbo.TR_RT_MOUVEMENT_Immuabilite
ON dbo.RT_MOUVEMENT
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Détecter les mouvements dont MV_Point passe de 1 → 0
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON i.MV_Id = d.MV_Id
        WHERE d.MV_Point = 1 AND i.MV_Point = 0
          AND EXISTS (
              SELECT 1 FROM dbo.RT_AFFECTATION a
              WHERE a.MV_Id  = i.MV_Id
                AND a.DT_Id IS NOT NULL
          )
    )
    BEGIN
        DECLARE @mvIdPoint INT, @dtIdPoint INT;
        SELECT TOP 1
            @mvIdPoint = i.MV_Id,
            @dtIdPoint = (SELECT TOP 1 a.DT_Id FROM dbo.RT_AFFECTATION a
                          WHERE a.MV_Id = i.MV_Id AND a.DT_Id IS NOT NULL)
        FROM inserted i
        JOIN deleted d ON i.MV_Id = d.MV_Id
        WHERE d.MV_Point = 1 AND i.MV_Point = 0
          AND EXISTS (SELECT 1 FROM dbo.RT_AFFECTATION a
                      WHERE a.MV_Id = i.MV_Id AND a.DT_Id IS NOT NULL);

        DECLARE @msgPoint NVARCHAR(500) =
            N'Opération interdite — le règlement est inclus dans la déclaration TVA n° '
            + CAST(@dtIdPoint AS NVARCHAR(20))
            + N'. Le dérapprochement est refusé tant que la déclaration est clôturée.';
        THROW 50028, @msgPoint, 1;
    END;

    -- Détecter les mouvements dont MV_Compta passe de 1 → 0
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON i.MV_Id = d.MV_Id
        WHERE d.MV_Compta = 1 AND i.MV_Compta = 0
          AND EXISTS (
              SELECT 1 FROM dbo.RT_AFFECTATION a
              WHERE a.MV_Id  = i.MV_Id
                AND a.DT_Id IS NOT NULL
          )
    )
    BEGIN
        DECLARE @mvIdCompta INT, @dtIdCompta INT;
        SELECT TOP 1
            @mvIdCompta = i.MV_Id,
            @dtIdCompta = (SELECT TOP 1 a.DT_Id FROM dbo.RT_AFFECTATION a
                           WHERE a.MV_Id = i.MV_Id AND a.DT_Id IS NOT NULL)
        FROM inserted i
        JOIN deleted d ON i.MV_Id = d.MV_Id
        WHERE d.MV_Compta = 1 AND i.MV_Compta = 0
          AND EXISTS (SELECT 1 FROM dbo.RT_AFFECTATION a
                      WHERE a.MV_Id = i.MV_Id AND a.DT_Id IS NOT NULL);

        DECLARE @msgCompta NVARCHAR(500) =
            N'Opération interdite — le règlement est inclus dans la déclaration TVA n° '
            + CAST(@dtIdCompta AS NVARCHAR(20))
            + N'. La décomptabilisation est refusée tant que la déclaration est clôturée.';
        THROW 50028, @msgCompta, 1;
    END;
END;
GO
