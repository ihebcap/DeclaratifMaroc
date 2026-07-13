-- TASK-077 : colonnes EC_Id / MV_Id sur DM_LGTVA — clé de revalidation ciblée des lignes
-- déjà figées (snapshot RT_ECHEANCE/RT_MOUVEMENT au moment du figeage). Sans ces deux
-- colonnes, une ligne déjà écrite dans DM_LGTVA ne peut être rattachée à aucune source
-- (ni au cache de ventilation GRC_VENTILATION_SAGE_CACHE par EC_Id, ni à l'état de
-- rapprochement courant RT_MOUVEMENT.MV_Point par MV_Id) : le garde-fou TASK-072 ne
-- protège alors que le chargement initial, jamais une resynchronisation ultérieure
-- (cas réel : EC_Id=21473/FC2501717, figé avant l'entrée en vigueur du correctif).
--
-- NOT NULL DEFAULT 0 (même convention que EcType/Prorata/MontantAffecte, TASK-055/057) :
-- LigneCandidate.EC_Id / MV_Id sont des `int` non-nullables côté C#, Dapper échouerait au
-- premier SELECT * si une ligne historique portait NULL. 0 = valeur inconnue pour les
-- lignes figées AVANT ce correctif (jamais un vrai EC_Id/MV_Id Sage, qui sont toujours
-- des identifiants positifs) — la revalidation TASK-077 ignore explicitement ces lignes
-- à EC_Id=0/MV_Id=0 (rien à vérifier, aucune clé connue), sans jamais les signaler à tort.
--
-- Idempotent : n'ajoute chaque colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'EC_Id'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [EC_Id] INT NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'MV_Id'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [MV_Id] INT NOT NULL DEFAULT 0;
END;
GO
