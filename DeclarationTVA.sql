-- =====================================================================
-- Tables de persistance de la Declaration TVA
-- A executer UNE FOIS dans la base SQL Server GRF (ex: GR_EMA_DISTRIBUTION).
-- Types alignes sur ce que l'application ecrit :
--   Id              -> NVARCHAR (Guid stocke en texte)
--   dates           -> DATETIME2
--   enums (Type...) -> INT
--   montants        -> FLOAT
-- =====================================================================

IF OBJECT_ID('dbo.DeclarationEntete', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeclarationEntete (
        Id            NVARCHAR(36)  NOT NULL PRIMARY KEY,
        Numero        NVARCHAR(50)  NULL,
        SocieteId     NVARCHAR(20)  NULL,
        Exercice      INT           NULL,
        Type          INT           NULL,
        Periode       INT           NULL,
        Statut        INT           NULL,
        DateCreation  DATETIME2     NULL,
        DateCloture   DATETIME2     NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeclarationEntete_Numero')
    CREATE UNIQUE INDEX IX_DeclarationEntete_Numero
        ON dbo.DeclarationEntete (Numero) WHERE Numero IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeclarationEntete_Unicite')
    CREATE UNIQUE INDEX IX_DeclarationEntete_Unicite
        ON dbo.DeclarationEntete (SocieteId, Exercice, Periode, Type);
GO

IF OBJECT_ID('dbo.LigneCandidate', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LigneCandidate (
        Id                      NVARCHAR(36)  NOT NULL PRIMARY KEY,
        DeclarationId           NVARCHAR(36)  NULL,
        Etat                    INT           NULL,
        Domaine                 NVARCHAR(50)  NULL,
        MotifRejet              NVARCHAR(500) NULL,
        NumeroFacture           NVARCHAR(100) NULL,
        TiersNom                NVARCHAR(255) NULL,
        TiersIdentifiantFiscal  NVARCHAR(100) NULL,
        TiersICE                NVARCHAR(100) NULL,
        HT                      FLOAT         NULL,
        Taux                    FLOAT         NULL,
        TVA                     FLOAT         NULL,
        TTC                     FLOAT         NULL,
        ModePaiement            NVARCHAR(100) NULL,
        DatePaiement            DATETIME2     NULL,
        DateFacture             DATETIME2     NULL,
        Source                  NVARCHAR(100) NULL,
        NumeroRapprochement     NVARCHAR(100) NULL,
        CONSTRAINT FK_LigneCandidate_Declaration
            FOREIGN KEY (DeclarationId) REFERENCES dbo.DeclarationEntete(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LigneCandidate_DeclarationId_Domaine')
    CREATE INDEX IX_LigneCandidate_DeclarationId_Domaine
        ON dbo.LigneCandidate (DeclarationId, Domaine);
GO
