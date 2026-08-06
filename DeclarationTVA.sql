-- =====================================================================
-- Script d'installation -- Declaration TVA (GRF)
-- A executer UNE FOIS par un DBA/sysadmin sur l'instance SQL Server cible.
-- Idempotent (IF NOT EXISTS partout) : peut etre rejoue sans risque lors
-- d'une mise a jour pour rattraper un schema en retard.
--
-- Contenu (dans l'ordre), fusion de tous les scripts SQL du repo
-- (racine DeclarationTVA.sql + Declaration.Infrastructure/SQL/*.sql) :
--   1. Tables de persistance : DM_ENTTVA, DM_LGTVA, DM_SELECTION_REGLEMENT,
--      DM_VENTILATION_SAGE_CACHE, DM_PARAM_DELAIPAIEMENT_SOCIETE,
--      DM_REPRISE_DELAIPAIEMENT, DM_SOLDE_INITIAL_TVA + toutes les migrations historiques
--      (TASK-025/055/057/064/065/066/072/076/077/078/094/097/118/128/161/189/198).
--   2. Trigger d'immuabilite (TASK-064) sur les tables ERP existantes
--      RT_AFFECTATION / RT_MOUVEMENT.
--   3. Login + droits SQL du compte applicatif (TASK-114, moindre privilege)
--      pour les 3 connexions de connections.json (GrfConnection,
--      SageConnection, PersistenceConnection).
--
-- Types alignes sur ce que l'application ecrit :
--   Id              -> NVARCHAR (Guid stocke en texte)
--   dates           -> DATETIME2
--   enums (Type...) -> INT
--   montants        -> DECIMAL(18,6) (persistance) / DECIMAL(18,4) (cache Sage)
--
-- A PERSONNALISER avant execution (rechercher/remplacer dans ce fichier) :
--   GR_EMA_DISTRIBUTION      -> nom reel de la base Grf/persistance chez le client
--   BASE_SAGE                -> nom reel de la base Sage chez le client
--   decl_tva_app             -> nom de login SQL souhaite pour l'application
--   ChangeMe_MotDePasseFort! -> mot de passe reel (fort), a reporter ensuite
--                               dans connections.json (User Id / Password)
--   1 (dans "SET SO_Id = 1")-> TASK-118 : SO_Id de la societe UNIQUE deja
--                               configuree chez ce client avant cette migration
--                               (seul cas possible avant le multi-Sage) -- sert
--                               a retro-remplir la nouvelle colonne SO_Id du
--                               cache de ventilation Sage sur les lignes deja
--                               en cache
--
-- Invocation (le script fixe lui-meme son contexte de base via USE -- pas
-- besoin de -d) :
--   sqlcmd -S SERVEUR_SQL -U UTILISATEUR -P MOT_DE_PASSE -i DeclarationTVA.sql
--
-- Hypothese (alignee sur l'exemple de connections.json dans LANCEMENT_DEV.md) :
-- GrfConnection et PersistenceConnection pointent vers LA MEME base -> un seul
-- login suffit pour les deux. Si le client impose des comptes distincts
-- (lecture ERP vs ecriture persistance), dupliquer la section 3b ci-dessous
-- avec un second login.
-- =====================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- =====================================================================
-- 1. Base GRF (GrfConnection + PersistenceConnection) : ex. GR_EMA_DISTRIBUTION
-- =====================================================================
USE GR_EMA_DISTRIBUTION;
GO

-- ---------------------------------------------------------------------
-- 1a. Migration / Renommage (TASK-065) : sp_rename des tables/index/contraintes
-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- 1b. Creation des tables DM_ENTTVA / DM_LGTVA
-- ---------------------------------------------------------------------
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
        DateCloture   DATETIME2     NULL,
        DT_Id         INT           NULL -- TASK-094 : miroir de RT_AFFECTATION.DT_Id pose a la cloture
    );
END;
GO

-- TASK-094 (Option B) — colonne ajoutee apres le premier deploiement (base deja creee sans elle) :
-- persiste sur DM_ENTTVA la meme valeur DT_Id posee sur RT_AFFECTATION a la cloture, pour un
-- JOIN direct RT_AFFECTATION.DT_Id = DM_ENTTVA.DT_Id sans recalcul cote diagnostic. Colonne
-- additive, NULLABLE, sur DM_ENTTVA UNIQUEMENT (jamais sur RT_* partagees). Ne resout pas
-- retroactivement les declarations closes AVANT cette migration (DT_Id reste NULL pour elles).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_ENTTVA') AND name = 'DT_Id')
    ALTER TABLE dbo.DM_ENTTVA ADD DT_Id INT NULL;
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
        EC_Id                   INT           NOT NULL DEFAULT 0, -- TASK-077 : cle de revalidation (snapshot RT_ECHEANCE)
        MV_Id                   INT           NOT NULL DEFAULT 0, -- TASK-077 : cle de revalidation (snapshot RT_MOUVEMENT)
        IncoherenceValidee      BIT           NOT NULL DEFAULT 0, -- TASK-078
        IncoherenceValideePar   NVARCHAR(200) NULL,                -- TASK-078
        IncoherenceValideeLe    DATETIME      NULL,                -- TASK-078
        CONSTRAINT FK_DM_LGTVA_DM_ENTTVA
            FOREIGN KEY (DeclarationId) REFERENCES dbo.DM_ENTTVA(Id)
    );
END;
GO

-- TASK-055 — colonnes ajoutees apres le premier deploiement (base deja creee sans elles) :
-- exposent Prorata et MontantAffecte (deja calcules par Declaration.Core) pour l'ecran ②.
-- NOT NULL DEFAULT 0 (comme EcType ci-dessous) — indispensable : LigneCandidate.Prorata/
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

-- TASK-077 — cle de revalidation ciblee des lignes deja figees (snapshot RT_ECHEANCE/RT_MOUVEMENT
-- au moment du figeage). 0 = valeur inconnue pour les lignes figees AVANT ce correctif (jamais un
-- vrai EC_Id/MV_Id Sage, toujours des identifiants positifs) — la revalidation ignore ces lignes.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'EC_Id')
    ALTER TABLE dbo.DM_LGTVA ADD EC_Id INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'MV_Id')
    ALTER TABLE dbo.DM_LGTVA ADD MV_Id INT NOT NULL DEFAULT 0;
GO

-- TASK-078 — tracabilite de la decision PO sur une ligne signalee incoherente (TASK-077).
-- IncoherenceValidee NOT NULL DEFAULT 0 : toute ligne existante est consideree NON validee par
-- defaut. IncoherenceValideePar/Le restent NULL tant qu'aucune validation n'a eu lieu.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'IncoherenceValidee')
    ALTER TABLE dbo.DM_LGTVA ADD IncoherenceValidee BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'IncoherenceValideePar')
    ALTER TABLE dbo.DM_LGTVA ADD IncoherenceValideePar NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'IncoherenceValideeLe')
    ALTER TABLE dbo.DM_LGTVA ADD IncoherenceValideeLe DATETIME NULL;
GO

-- TASK-161 — code activite TVA resolu en cascade (surcharge ligne > defaut tiers
-- P_SOCIETECODEACTIVITETIERS > colonne Sage CT_APE > vide), persiste au premier figeage pour
-- combler le gap deja documente (LigneCandidate ne portait aucun CodeActivite jusqu'ici, impactait
-- l'export de depot TASK-155 et le recap par activite de l'export de controle TASK-160). NULL (pas
-- NOT NULL DEFAULT '') : meme convention que TiersNom/NumeroFacture/ModePaiement ci-dessus.
-- CodeActiviteModifieManuellement/Par/Le : tracabilite de la surcharge manuelle par ligne, meme
-- pattern que IncoherenceValidee/Par/Le ci-dessus (TASK-078).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'CodeActivite')
    ALTER TABLE dbo.DM_LGTVA ADD CodeActivite NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'CodeActiviteModifieManuellement')
    ALTER TABLE dbo.DM_LGTVA ADD CodeActiviteModifieManuellement BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'CodeActiviteModifiePar')
    ALTER TABLE dbo.DM_LGTVA ADD CodeActiviteModifiePar NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'CodeActiviteModifieLe')
    ALTER TABLE dbo.DM_LGTVA ADD CodeActiviteModifieLe DATETIME NULL;
GO

-- TASK-189 — comble le trou laisse par TASK-187 : Reference (RT_ECHEANCE.DO_Reference) etait
-- propagee jusqu'a ConstructeurDeclaration/Exporter.cs mais jamais persistee sur DM_LGTVA, donc jamais
-- lue par les deux methodes reellement cablees cote API (ConstruireModeleExportAsync/
-- ConstruireModeleControleAsync, qui lisent LigneCandidate/DM_LGTVA, jamais ConstructeurDeclaration).
-- NVARCHAR(200) : source NVARCHAR(MAX), mais longueur max observee reelle = 25 caracteres (verifie sur
-- GR_EMA_DISTRIBUTION) -- marge large sans reprendre MAX. NULL (pas NOT NULL DEFAULT '') : meme
-- convention que NumeroFacture/TiersNom ci-dessus.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'Reference')
    ALTER TABLE dbo.DM_LGTVA ADD Reference NVARCHAR(200) NULL;
GO

-- TASK-198 (gap comble, cf. 012_DM_LGTVA_CodeTaxe.sql) : CodeTaxe (F_TAXE.TA_Code) propagee jusqu'au
-- modele/DTOs/Excel/front mais jamais persistee sur DM_LGTVA -- SaveLignesCandidatesAsync ne l'ecrivait
-- pas, donc GetLignesAsync (SELECT *) la relisait toujours vide.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_LGTVA') AND name = 'CodeTaxe')
    ALTER TABLE dbo.DM_LGTVA ADD CodeTaxe NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_LGTVA_DeclarationId_Domaine')
    CREATE INDEX IX_DM_LGTVA_DeclarationId_Domaine
        ON dbo.DM_LGTVA (DeclarationId, Domaine);
GO

-- ---------------------------------------------------------------------
-- 1c. Migration des bases existantes (de FLOAT vers DECIMAL(18,6))
-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- 1d. Migration des bases existantes (SocieteId de NVARCHAR vers INT) (TASK-066)
-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- 1e. Creation de la table DM_SELECTION_REGLEMENT (TASK-097)
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.DM_SELECTION_REGLEMENT', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_SELECTION_REGLEMENT (
        DeclarationId    NVARCHAR(36)  NOT NULL,
        NumeroReglement  NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_DM_SELECTION_REGLEMENT PRIMARY KEY (DeclarationId, NumeroReglement),
        CONSTRAINT FK_DM_SELECTION_REGLEMENT_DM_ENTTVA
            FOREIGN KEY (DeclarationId) REFERENCES dbo.DM_ENTTVA(Id) ON DELETE CASCADE
    );
END;
GO

-- ---------------------------------------------------------------------
-- 1f. Migration / Renommage (TASK-118) : GRC_VENTILATION_SAGE_CACHE -> DM_VENTILATION_SAGE_CACHE
--     (conformite nommage DM_* deja etabli par TASK-065). sp_rename idempotent
--     (jamais un nouveau CREATE TABLE) pour preserver les donnees deja en cache.
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.GRC_VENTILATION_SAGE_CACHE', 'U') IS NOT NULL AND OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE', 'U') IS NULL
BEGIN
    EXEC sp_rename 'dbo.GRC_VENTILATION_SAGE_CACHE', 'DM_VENTILATION_SAGE_CACHE';
END;
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_GRC_VENTILATION_SAGE_CACHE' AND parent_object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE'))
   AND NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_DM_VENTILATION_SAGE_CACHE')
BEGIN
    EXEC sp_rename 'dbo.PK_GRC_VENTILATION_SAGE_CACHE', 'PK_DM_VENTILATION_SAGE_CACHE';
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GRC_VENTILATION_SAGE_CACHE_ECId' AND object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_VENTILATION_SAGE_CACHE_ECId' AND object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE'))
BEGIN
    EXEC sp_rename 'dbo.DM_VENTILATION_SAGE_CACHE.IX_GRC_VENTILATION_SAGE_CACHE_ECId', 'IX_DM_VENTILATION_SAGE_CACHE_ECId', 'INDEX';
END;
GO

-- ---------------------------------------------------------------------
-- 1g. Creation de la table DM_VENTILATION_SAGE_CACHE (TASK-024/072/076/118)
--     Cache des ventilations Sage (EC_Type = 0). Cle : (SO_Id, EC_Id, Taux) — une
--     ligne par bucket TVA par facture PAR SOCIETE. Ne jamais ecrire dans la base
--     GRFN pour cette table (cible : PersistenceConnection, meme si physiquement
--     dans la meme base que DM_* dans l'exemple connections.json).
--
--     TASK-118 : SO_Id ajoute a la cle -- EC_Id (identifiant interne Sage de
--     l'echeance) n'est unique qu'a l'interieur d'une seule base Sage ; des que
--     GRF adresse plusieurs bases Sage (une par SO_Id, cf. P_SOCIETE.SO_ErpDb),
--     un meme EC_Id peut designer deux factures totalement differentes. SO_Id
--     est une reference LOGIQUE a P_SOCIETE.SO_Id, volontairement SANS
--     contrainte FOREIGN KEY (exigence PO explicite : ne pas impacter le modele
--     EF de l'application principale, proprietaire de P_SOCIETE).
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_VENTILATION_SAGE_CACHE (
        SO_Id            INT             NOT NULL, -- TASK-118 : reference logique P_SOCIETE.SO_Id, sans FK
        EC_Id            INT             NOT NULL,
        Taux             DECIMAL(18,4)   NOT NULL,
        BaseHT           DECIMAL(18,4)   NOT NULL,
        MontantTva       DECIMAL(18,4)   NOT NULL,
        TTC              DECIMAL(18,4)   NOT NULL,
        CodeTaxe         NVARCHAR(50)    NOT NULL DEFAULT '',
        TotalHT          DECIMAL(18,4)   NOT NULL,
        TotalTva         DECIMAL(18,4)   NOT NULL,
        TotalTtc         DECIMAL(18,4)   NOT NULL,
        -- Token de paiement capture a l'ecriture. NULL = facture lue mais non
        -- rattachee a un reglement pointe : ventilation "brute" conservee
        -- (reutilisable pour l'affichage) mais non declarable.
        Token_MV_Id      INT             NULL,
        Token_MV_Point   INT             NULL,
        DateLecture      DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        Source           NVARCHAR(100)   NOT NULL DEFAULT 'OM',
        -- TASK-072 : motif d'erreur, porte par une ligne sentinelle unique
        -- (Taux=-1, CodeTaxe='ERREUR') quand une piece est exclue de la valorisation.
        MotifErreur      NVARCHAR(500)   NULL,
        -- TASK-076 : montants BRUTS Sage (tels que lus, AVANT exclusion), portes par
        -- la meme ligne sentinelle d'erreur. Jamais renseignes sur une ligne reelle.
        BrutHT           DECIMAL(18,4)   NULL,
        BrutTva          DECIMAL(18,4)   NULL,
        BrutParafiscale  DECIMAL(18,4)   NULL,
        BrutTtc          DECIMAL(18,4)   NULL,
        CONSTRAINT PK_DM_VENTILATION_SAGE_CACHE PRIMARY KEY (SO_Id, EC_Id, Taux)
    );
END;
GO

-- Migrations additives (bases deja creees avant TASK-072/076) :
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'MotifErreur')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD MotifErreur NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'BrutHT')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD BrutHT DECIMAL(18,4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'BrutTva')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD BrutTva DECIMAL(18,4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'BrutParafiscale')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD BrutParafiscale DECIMAL(18,4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'BrutTtc')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD BrutTtc DECIMAL(18,4) NULL;
GO

-- Migration : rendre le token de paiement NULLable (bases creees avant ce changement) —
-- on met en cache TOUTES les factures lues en OM, meme sans reglement pointe.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'Token_MV_Id' AND is_nullable = 0)
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ALTER COLUMN Token_MV_Id INT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'Token_MV_Point' AND is_nullable = 0)
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ALTER COLUMN Token_MV_Point INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DM_VENTILATION_SAGE_CACHE_ECId')
    CREATE INDEX IX_DM_VENTILATION_SAGE_CACHE_ECId
        ON dbo.DM_VENTILATION_SAGE_CACHE (EC_Id);
GO

-- ---------------------------------------------------------------------
-- 1h. Migration TASK-118 : ajout de SO_Id a la cle du cache (bases renommees
--     ci-dessus depuis GRC_VENTILATION_SAGE_CACHE, donc creees AVANT SO_Id).
--     Back-fill = SO_Id unique deja configure chez ce client avant cette task
--     (seul cas possible en mono-Sage) ; personnaliser la valeur "1" ci-dessous
--     si la societe unique utilisee n'est pas SO_Id=1.
-- ---------------------------------------------------------------------
-- NOTE : chaque ALTER TABLE ADD COLUMN doit rester dans un batch SEPARE (GO) de toute
-- instruction qui reference ensuite cette colonne -- SQL Server compile/lie un batch entier
-- AVANT de l'executer ; une UPDATE/ALTER COLUMN sur SO_Id DANS LE MEME BATCH que son ADD
-- echoue avec "Invalid column name 'SO_Id'" (verifie en conditions reelles, .\sql2022).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'SO_Id')
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD SO_Id INT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE') AND name = 'SO_Id' AND is_nullable = 1)
BEGIN
    UPDATE dbo.DM_VENTILATION_SAGE_CACHE SET SO_Id = 1 WHERE SO_Id IS NULL;
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ALTER COLUMN SO_Id INT NOT NULL;
END;
GO

-- La cle primaire (EC_Id, Taux) precedente ne couvre plus l'unicite reelle (deux societes
-- peuvent desormais partager un EC_Id) : elargie a (SO_Id, EC_Id, Taux). Guard : ne rejoue
-- QUE si la PK existe encore SANS SO_Id (idempotent, jamais re-declenche une fois migree).
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_DM_VENTILATION_SAGE_CACHE' AND parent_object_id = OBJECT_ID('dbo.DM_VENTILATION_SAGE_CACHE'))
   AND NOT EXISTS (
       SELECT 1 FROM sys.key_constraints kc
       JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
       JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE kc.name = 'PK_DM_VENTILATION_SAGE_CACHE' AND c.name = 'SO_Id'
   )
BEGIN
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE DROP CONSTRAINT PK_DM_VENTILATION_SAGE_CACHE;
    ALTER TABLE dbo.DM_VENTILATION_SAGE_CACHE ADD CONSTRAINT PK_DM_VENTILATION_SAGE_CACHE PRIMARY KEY (SO_Id, EC_Id, Taux);
END;
GO

-- ---------------------------------------------------------------------
-- 1i. Delai de Paiement Maroc (TASK-128) : parametre "date de mise en route" par societe +
--     reprise manuelle par echeance. DEUX TABLES NEUVES, propriete exclusive GRF -- AUCUNE
--     modification de P_SOCIETE (partagee avec l'app legacy et le reste de GRF, exigence PO
--     explicite). SO_Id/EC_Id/UT_Id sont des references LOGIQUES a P_SOCIETE.SO_Id /
--     RT_ECHEANCE.EC_Id / P_UTILISATEUR.UT_Id, volontairement SANS contrainte FOREIGN KEY (meme
--     principe que DM_VENTILATION_SAGE_CACHE.SO_Id ci-dessus, section 1g).
--
--     DM_PARAM_DELAIPAIEMENT_SOCIETE : une ligne par societe ayant configure sa bascule. ABSENCE
--     de ligne = "pas encore configure" (jamais une date par defaut arbitraire) -- le repository
--     (DelaiPaiementBootstrapRepository.GetAsync) renvoie null dans ce cas, jamais une valeur de
--     repli silencieuse.
--
--     DM_REPRISE_DELAIPAIEMENT : reprise manuelle ponctuelle "retard deja connu/declare jusqu'au
--     [date]", par echeance precise (SO_Id, EC_Id). Cle primaire (SO_Id, EC_Id) : une seule reprise
--     active par echeance (une nouvelle saisie ecrase la precedente via MERGE, pas d'historique de
--     versions -- decision worker, aucune exigence PO explicite sur l'historisation des reprises ;
--     a signaler si un besoin d'audit multi-versions emerge cote consommateurs).
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.DM_PARAM_DELAIPAIEMENT_SOCIETE', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_PARAM_DELAIPAIEMENT_SOCIETE (
        SO_Id           INT             NOT NULL, -- reference logique P_SOCIETE.SO_Id, sans FK
        DateMiseEnRoute DATETIME2       NOT NULL,
        UT_Id           INT             NULL,      -- reference logique P_UTILISATEUR.UT_Id, sans FK
        DateSaisie      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DM_PARAM_DELAIPAIEMENT_SOCIETE PRIMARY KEY (SO_Id)
    );
END;
GO

IF OBJECT_ID('dbo.DM_REPRISE_DELAIPAIEMENT', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_REPRISE_DELAIPAIEMENT (
        SO_Id                    INT             NOT NULL, -- reference logique P_SOCIETE.SO_Id, sans FK
        EC_Id                    INT             NOT NULL, -- reference logique RT_ECHEANCE.EC_Id, sans FK
        DateDejaDeclareeJusquau  DATETIME2       NOT NULL,
        UT_Id                    INT             NULL,      -- reference logique P_UTILISATEUR.UT_Id, sans FK
        DateSaisie               DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DM_REPRISE_DELAIPAIEMENT PRIMARY KEY (SO_Id, EC_Id)
    );
END;
GO

-- ---------------------------------------------------------------------
-- 1j. Solde initial GRF (RT_ECHEANCE.EC_Type = 4, cf. DONE_DETAIL/TASK-025) : le montant du
--     solde n'a aucun detail HT/TVA/taux cote Sage (connu en TTC seul). Decision PO : integrer
--     ces lignes a la declaration TVA moyennant une saisie MANUELLE du comptable (taux + montant
--     de TVA), plutot que les eliminer silencieusement comme auparavant. Cette table persiste
--     cette saisie, par societe et par echeance (EC_Id) -- l'orchestrateur la recharge en batch
--     avant de construire la declaration (OrchestrateurDeclaration.Traiter).
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.DM_SOLDE_INITIAL_TVA', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_SOLDE_INITIAL_TVA (
        SO_Id       INT             NOT NULL,
        EC_Id       INT             NOT NULL,
        Taux        DECIMAL(18,6)   NOT NULL,
        MontantTva  DECIMAL(18,6)   NOT NULL,
        SaisiPar    NVARCHAR(200)   NULL,
        SaisiLe     DATETIME        NOT NULL,
        CONSTRAINT PK_DM_SOLDE_INITIAL_TVA PRIMARY KEY (SO_Id, EC_Id)
    );
END;
GO

-- =====================================================================
-- 2. Trigger d'immuabilite TOTALE d'une affectation declaree (TASK-064)
--    Cible : tables ERP existantes RT_AFFECTATION / RT_MOUVEMENT (base GRF).
-- =====================================================================

-- ---------------------------------------------------------------------
-- 2a. Index utile pour les triggers RT_MOUVEMENT (EXISTS par MV_Id)
--     IMPORTANT : index NON filtre volontairement. Un index filtre
--     (WHERE DT_Id IS NOT NULL) impose SET QUOTED_IDENTIFIER ON a TOUT
--     writer de RT_AFFECTATION (msg 1934) — or GRFN/Sage legacy peuvent
--     ecrire avec QUOTED_IDENTIFIER OFF, ce qui casserait leurs flux
--     legitimes. Un index ordinaire (MV_Id, DT_Id) sert le seek du trigger
--     sans imposer d'option de session aux applis existantes.
-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- 2b. Trigger immuabilite RT_AFFECTATION
--     - Interdit DELETE d'une affectation DT_Id IS NOT NULL.
--     - Interdit TOUTE modification d'une affectation declaree (DT_Id NOT NULL).
--     - AUTORISE UNIQUEMENT la transition de de-tamponnage PUR (DT_Id : valeur → NULL),
--       si toutes les autres colonnes protegees restent inchangees.
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.TR_RT_AFFECTATION_Immuabilite', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RT_AFFECTATION_Immuabilite;
GO

CREATE TRIGGER dbo.TR_RT_AFFECTATION_Immuabilite
ON dbo.RT_AFFECTATION
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- ── Bloquer DELETE d'une affectation declaree ──────────────────
    IF EXISTS (
        SELECT 1 FROM deleted d
        WHERE d.DT_Id IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.AF_Id = d.AF_Id)
    )
    BEGIN
        DECLARE @dtIdDel INT;
        SELECT TOP 1 @dtIdDel = d.DT_Id FROM deleted d
        WHERE d.DT_Id IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.AF_Id = d.AF_Id);

        THROW 50028, N'Opération interdite — cette affectation est incluse dans la déclaration TVA et ne peut pas être supprimée. Rouvrez la déclaration avant toute modification.', 1;
    END;

    -- ── Bloquer tout UPDATE si DT_Id NOT NULL ──────────────────────
    -- On interdit toute modification si la ligne ÉTAIT déjà déclarée (d.DT_Id IS NOT NULL).
    -- La seule exception autorisée est le dé-tamponnage PUR (transition DT_Id : valeur → NULL)
    -- où absolument aucune autre colonne protégée n'a été modifiée.
    IF EXISTS (
        SELECT 1
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

-- ---------------------------------------------------------------------
-- 2c. Trigger immuabilite RT_MOUVEMENT
--     - Si le mouvement possede ≥1 affectation DT_Id IS NOT NULL :
--         • Interdit MV_Point 1 → 0 (derapprochement)
--         • Interdit MV_Compta 1 → 0 (decomptabilisation)
--     - Tout le reste passe (mise a jour neutre, tamponnage…).
-- ---------------------------------------------------------------------
IF OBJECT_ID('dbo.TR_RT_MOUVEMENT_Immuabilite', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_RT_MOUVEMENT_Immuabilite;
GO

CREATE TRIGGER dbo.TR_RT_MOUVEMENT_Immuabilite
ON dbo.RT_MOUVEMENT
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

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

-- =====================================================================
-- 3. Droits SQL Server -- compte applicatif Declaration TVA (TASK-114)
--    Compte dedie a moindre privilege pour connections.json -- ne JAMAIS
--    utiliser un compte admin/sa pour l'application.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 3a. Login (niveau instance)
-- ---------------------------------------------------------------------
USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'decl_tva_app')
BEGIN
    CREATE LOGIN decl_tva_app
        WITH PASSWORD = N'ChangeMe_MotDePasseFort!',
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
END;
GO

-- ---------------------------------------------------------------------
-- 3b. Base GRF (GrfConnection + PersistenceConnection)
--     - Lecture large (db_datareader) : l'ecran Selection lit les tables ERP
--       existantes (reglements, factures, rapprochements...).
--     - Ecriture CIBLEE :
--         * les 6 tables de persistance ci-dessus (DM_ENTTVA, DM_LGTVA,
--           DM_SELECTION_REGLEMENT, DM_VENTILATION_SAGE_CACHE,
--           DM_PARAM_DELAIPAIEMENT_SOCIETE, DM_REPRISE_DELAIPAIEMENT) ;
--         * UPDATE sur la seule colonne RT_AFFECTATION.DT_Id (pose/retrait du
--           tampon de cloture — DeclarationRepository.TamponnerAffectationsAsync/
--           DetamponnerAffectationsAsync). Jamais d'ecriture sur RT_MOUVEMENT ni
--           sur une autre colonne ERP.
-- ---------------------------------------------------------------------
USE GR_EMA_DISTRIBUTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'decl_tva_app')
BEGIN
    CREATE USER decl_tva_app FOR LOGIN decl_tva_app;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'decl_tva_app'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER decl_tva_app;
END;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_ENTTVA TO decl_tva_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_LGTVA TO decl_tva_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_SELECTION_REGLEMENT TO decl_tva_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_VENTILATION_SAGE_CACHE TO decl_tva_app;
GO

-- TASK-128 : les 2 tables neuves du bootstrap Delai de Paiement Maroc.
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_PARAM_DELAIPAIEMENT_SOCIETE TO decl_tva_app;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_REPRISE_DELAIPAIEMENT TO decl_tva_app;
GO

-- TASK-025 : saisie manuelle solde initial (section 1j ci-dessus).
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.DM_SOLDE_INITIAL_TVA TO decl_tva_app;
GO

-- Moindre privilege : autorisation d'ecriture limitee a la colonne DT_Id (pas
-- tout RT_AFFECTATION). Le trigger TR_RT_AFFECTATION_Immuabilite (section 2b)
-- reste la barriere metier ; ce GRANT ne fait qu'ouvrir le droit technique.
GRANT UPDATE (DT_Id) ON dbo.RT_AFFECTATION TO decl_tva_app;
GO

-- ---------------------------------------------------------------------
-- 3c. Base Sage (SageConnection) -- lecture seule
--     Le worker OM (SageTaxReader.Console) accede a Sage principalement via
--     COM Objets Metiers, mais certaines lectures directes (ex. RT_HISTOCOMPTA
--     pour les FGR) passent par SageConnection. Aucune ecriture necessaire.
-- ---------------------------------------------------------------------
USE BASE_SAGE;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'decl_tva_app')
BEGIN
    CREATE USER decl_tva_app FOR LOGIN decl_tva_app;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'decl_tva_app'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER decl_tva_app;
END;
GO

-- =====================================================================
-- 4. Verification (a executer manuellement apres coup)
-- =====================================================================
-- SELECT dp.name AS [user], r.name AS [role]
-- FROM sys.database_role_members rm
-- JOIN sys.database_principals r  ON r.principal_id = rm.role_principal_id
-- JOIN sys.database_principals dp ON dp.principal_id = rm.member_principal_id
-- WHERE dp.name = 'decl_tva_app';
--
-- SELECT tp.name AS [table], pr.permission_name, pr.state_desc
-- FROM sys.database_permissions pr
-- JOIN sys.tables tp ON tp.object_id = pr.major_id
-- JOIN sys.database_principals dp ON dp.principal_id = pr.grantee_principal_id
-- WHERE dp.name = 'decl_tva_app';
