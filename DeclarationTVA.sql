-- =====================================================================
-- Tables de persistance de la Declaration TVA
-- A executer UNE FOIS dans la base SQL Server GRF (ex: GR_EMA_DISTRIBUTION).
-- Types alignes sur ce que l'application ecrit :
--   Id              -> NVARCHAR (Guid stocke en texte)
--   dates           -> DATETIME2
--   enums (Type...) -> INT
--   montants        -> DECIMAL(18,6)
-- =====================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =====================================================================
-- Migration / Renommage (TASK-065) : sp_rename des tables/index/contraintes
-- =====================================================================
IF OBJECT_ID('dbo.DeclarationEntete', 'U') IS NOT NULL AND OBJECT_ID('dbo.DM_ENTTVA', 'U') IS NULL
BEGIN
    EXEC sp_rename 'dbo.DeclarationEntete', 'DM_ENTTVA';
END;
GO

IF OBJECT_ID('dbo.LigneCandidate', 'U') IS NOT NULL AND OBJECT_ID('dbo.DM_LGTVA', 'U') IS NULL
BEGIN
    EXEC sp_rename 'dbo.LigneCandidate', 'DM_LGTVA';
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeclarationEntete_Numero' AND object_id = OBJECT_ID('dbo.DM_ENTTVA'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_ENTTVA_Numero' AND object_id = OBJECT_ID('dbo.DM_ENTTVA'))
BEGIN
    EXEC sp_rename 'dbo.DM_ENTTVA.IX_DeclarationEntete_Numero', 'IX_DM_ENTTVA_Numero', 'INDEX';
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeclarationEntete_Unicite' AND object_id = OBJECT_ID('dbo.DM_ENTTVA'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_ENTTVA_Unicite' AND object_id = OBJECT_ID('dbo.DM_ENTTVA'))
BEGIN
    EXEC sp_rename 'dbo.DM_ENTTVA.IX_DeclarationEntete_Unicite', 'IX_DM_ENTTVA_Unicite', 'INDEX';
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LigneCandidate_DeclarationId_Domaine' AND object_id = OBJECT_ID('dbo.DM_LGTVA'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_LGTVA_DeclarationId_Domaine' AND object_id = OBJECT_ID('dbo.DM_LGTVA'))
BEGIN
    EXEC sp_rename 'dbo.DM_LGTVA.IX_LigneCandidate_DeclarationId_Domaine', 'IX_DM_LGTVA_DeclarationId_Domaine', 'INDEX';
END;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LigneCandidate_Declaration')
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DM_LGTVA_DM_ENTTVA')
BEGIN
    EXEC sp_rename 'dbo.FK_LigneCandidate_Declaration', 'FK_DM_LGTVA_DM_ENTTVA';
END;
GO

-- =====================================================================
-- Creation des tables
-- =====================================================================
IF OBJECT_ID('dbo.DM_ENTTVA', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_ENTTVA (
        Id            NVARCHAR(36)  NOT NULL PRIMARY KEY,
        Numero        NVARCHAR(50)  NULL,
        SocieteId     INT           NULL,
        Exercice      INT           NULL,
        Type          INT           NULL,
        Periode       INT           NULL,
        Statut        INT           NULL,
        DateCreation  DATETIME2     NULL,
        DateCloture   DATETIME2     NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_ENTTVA_Numero')
    CREATE UNIQUE INDEX IX_DM_ENTTVA_Numero
        ON dbo.DM_ENTTVA (Numero) WHERE Numero IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_ENTTVA_Unicite')
    CREATE UNIQUE INDEX IX_DM_ENTTVA_Unicite
        ON dbo.DM_ENTTVA (SocieteId, Exercice, Periode, Type);
GO

IF OBJECT_ID('dbo.DM_LGTVA', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_LGTVA (
        Id                      NVARCHAR(36)  NOT NULL PRIMARY KEY,
        DeclarationId           NVARCHAR(36)  NULL,
        Etat                    INT           NULL,
        Domaine                 NVARCHAR(50)  NULL,
        MotifRejet              NVARCHAR(500) NULL,
        NumeroFacture           NVARCHAR(100) NULL,
        TiersNom                NVARCHAR(255) NULL,
        TiersIdentifiantFiscal  NVARCHAR(100) NULL,
        TiersICE                NVARCHAR(100) NULL,
        HT                      DECIMAL(18,6)  NULL,
        Taux                    DECIMAL(18,6)  NULL,
        TVA                     DECIMAL(18,6)  NULL,
        TTC                     DECIMAL(18,6)  NULL,
        Prorata                 DECIMAL(18,6)  NOT NULL DEFAULT 0,
        MontantAffecte          DECIMAL(18,6)  NOT NULL DEFAULT 0,
        ModePaiement            NVARCHAR(100) NULL,
        DatePaiement            DATETIME2     NULL,
        DateFacture             DATETIME2     NULL,
        Source                  NVARCHAR(100) NULL,
        NumeroRapprochement     NVARCHAR(100) NULL,
        EcType                  INT           NOT NULL DEFAULT 0, -- Origine valorisation TVA (0=Sage/OM, 111=FGR, 4=Solde initial)
        CONSTRAINT FK_DM_LGTVA_DM_ENTTVA
            FOREIGN KEY (DeclarationId) REFERENCES dbo.DM_ENTTVA(Id)
    );
END;
GO

-- TASK-055 — colonnes ajoutees apres le premier deploiement (base deja creee sans elles) :
-- exposent Prorata et MontantAffecte (deja calcules par Declaration.Core) pour l'ecran ②.
-- NOT NULL DEFAULT 0 (comme EcType ci-dessus) — indispensable : LigneCandidate.Prorata/
-- MontantAffecte sont des `decimal` non-nullables cote C#, Dapper leve une exception au premier
-- SELECT * si une ligne historique porte NULL. DEFAULT 0 sur ADD COLUMN retro-remplit les lignes
-- deja en base (0 = non valorise pour ces figees, coherent avec les 2 branches non-eligibles).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'Prorata')
    ALTER TABLE dbo.DM_LGTVA ADD Prorata DECIMAL(18,6) NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'MontantAffecte')
    ALTER TABLE dbo.DM_LGTVA ADD MontantAffecte DECIMAL(18,6) NOT NULL DEFAULT 0;
GO

-- TASK-057 — colonne ajoutee apres certains deploiements (bases creees avant que EcType soit
-- inclus dans le CREATE TABLE) :
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'EcType')
    ALTER TABLE dbo.DM_LGTVA ADD EcType INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_LGTVA_DeclarationId_Domaine')
    CREATE INDEX IX_DM_LGTVA_DeclarationId_Domaine
        ON dbo.DM_LGTVA (DeclarationId, Domaine);
GO

-- =====================================================================
-- Migration des bases existantes (de FLOAT vers DECIMAL(18,6))
-- =====================================================================
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'HT' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN HT DECIMAL(18,6) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'Taux' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN Taux DECIMAL(18,6) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'TVA' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN TVA DECIMAL(18,6) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'TTC' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN TTC DECIMAL(18,6) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'Prorata' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    DECLARE @ConstraintNameProrata NVARCHAR(256);
    SELECT @ConstraintNameProrata = d.name
    FROM sys.default_constraints d
    JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
    WHERE d.parent_object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'Prorata';

    IF @ConstraintNameProrata IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.DM_LGTVA DROP CONSTRAINT ' + @ConstraintNameProrata);
    END;

    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN Prorata DECIMAL(18,6) NOT NULL;
    ALTER TABLE dbo.DM_LGTVA ADD CONSTRAINT DF_DM_LGTVA_Prorata DEFAULT 0 FOR Prorata;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'MontantAffecte' AND (t.name <> 'decimal' OR c.precision <> 18 OR c.scale <> 6))
BEGIN
    DECLARE @ConstraintNameAffecte NVARCHAR(256);
    SELECT @ConstraintNameAffecte = d.name
    FROM sys.default_constraints d
    JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
    WHERE d.parent_object_id = OBJECT_ID('dbo.DM_LGTVA') AND c.name = 'MontantAffecte';

    IF @ConstraintNameAffecte IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.DM_LGTVA DROP CONSTRAINT ' + @ConstraintNameAffecte);
    END;

    ALTER TABLE dbo.DM_LGTVA ALTER COLUMN MontantAffecte DECIMAL(18,6) NOT NULL;
    ALTER TABLE dbo.DM_LGTVA ADD CONSTRAINT DF_DM_LGTVA_MontantAffecte DEFAULT 0 FOR MontantAffecte;
END;
GO

-- =====================================================================
-- Migration des bases existantes (SocieteId de NVARCHAR vers INT) (TASK-066)
-- =====================================================================
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id 
           WHERE c.object_id = OBJECT_ID('dbo.DM_ENTTVA') AND c.name = 'SocieteId' AND t.name <> 'int')
BEGIN
    -- 1. Réconciliation des données existantes (de '001' vers 1)
    UPDATE dbo.DM_ENTTVA SET SocieteId = '1' WHERE SocieteId = '001';

    -- 2. Suppression de l'index unique dépendant
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_ENTTVA_Unicite' AND object_id = OBJECT_ID('dbo.DM_ENTTVA'))
    BEGIN
        DROP INDEX IX_DM_ENTTVA_Unicite ON dbo.DM_ENTTVA;
    END;

    -- 3. Altération de la colonne
    ALTER TABLE dbo.DM_ENTTVA ALTER COLUMN SocieteId INT NULL;

    -- 4. Recréation de l'index unique
    EXEC('CREATE UNIQUE INDEX IX_DM_ENTTVA_Unicite ON dbo.DM_ENTTVA (SocieteId, Exercice, Periode, Type)');
END;
GO

