-- TASK-161 : code activité TVA résolu en cascade (surcharge manuelle ligne > défaut tiers
-- P_SOCIETECODEACTIVITETIERS > colonne Sage désignée (CT_APE) > vide) et persisté sur la ligne
-- au premier figeage, pour combler le gap déjà documenté (DeclarationWorkflowService.cs, gap
-- accepté par TASK-155/TASK-160 : LigneCandidate ne portait aucun CodeActivite jusqu'ici).
--
-- CodeActivite NULL (jamais NOT NULL DEFAULT '') : même convention que TiersNom/NumeroFacture/
-- ModePaiement/Source sur cette même table (toutes NVARCHAR NULL dans le CREATE TABLE d'origine)
-- -- l'absence de valeur est déjà gérée "" côté consommateurs (jamais une exception Dapper sur
-- une colonne string, à la différence des colonnes decimal/int qui exigent NOT NULL DEFAULT 0).
--
-- CodeActiviteModifieManuellement/Par/Le : traçabilité de la surcharge manuelle par ligne
-- (écran ② Vérifier & Intégrer, décision PO TASK-161 point 5), MÊME PATTERN que
-- IncoherenceValidee/IncoherenceValideePar/IncoherenceValideeLe (TASK-078, 007_...sql) --
-- NOT NULL DEFAULT 0 pour le booléen (toute ligne existante n'est PAS considérée surchargée par
-- défaut), Par/Le restent NULL tant qu'aucune surcharge n'a eu lieu.
--
-- Idempotent : n'ajoute chaque colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRF (P_DECTVAACTIVITE/
-- P_SOCIETECODEACTIVITETIERS restent lues en lecture seule sur GrfConnection, jamais ici).

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'CodeActivite'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [CodeActivite] NVARCHAR(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'CodeActiviteModifieManuellement'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [CodeActiviteModifieManuellement] BIT NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'CodeActiviteModifiePar'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [CodeActiviteModifiePar] NVARCHAR(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'CodeActiviteModifieLe'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [CodeActiviteModifieLe] DATETIME NULL;
END;
GO
