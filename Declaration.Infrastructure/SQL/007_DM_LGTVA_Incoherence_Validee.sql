-- TASK-078 : traçabilité de la décision PO sur une ligne signalée incohérente (TASK-077).
-- Le signalement seul (LIGNE_FIGEE_A_REVERIFIER) ne suffit pas : le PO doit pouvoir
-- explicitement VALIDER l'incohérence (acceptée en connaissance de cause, tracée qui/quand)
-- ou la corriger/resynchroniser (nouvelle lecture Sage). Sans ces colonnes, une alerte déjà
-- vue reviendrait indéfiniment à chaque chargement de l'écran ② Affectations.
--
-- NOT NULL DEFAULT 0 pour IncoherenceValidee (même convention que EC_Id/MV_Id, TASK-077) :
-- toute ligne existante est considérée NON validée par défaut (jamais un silence pris pour
-- une validation implicite). IncoherenceValideePar/Le restent NULL tant qu'aucune validation
-- n'a eu lieu — pas de valeur inventée.
--
-- Idempotent : n'ajoute chaque colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'IncoherenceValidee'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [IncoherenceValidee] BIT NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'IncoherenceValideePar'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [IncoherenceValideePar] NVARCHAR(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'IncoherenceValideeLe'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [IncoherenceValideeLe] DATETIME NULL;
END;
GO
