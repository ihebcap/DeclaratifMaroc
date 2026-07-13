-- TASK-076 : colonnes des montants BRUTS Sage (tels que lus, AVANT exclusion) portées par la
-- ligne sentinelle d'erreur (Taux=-1, CodeTaxe='ERREUR') d'une pièce exclue pour incohérence
-- HT/TVA/TTC (TASK-072). Jamais renseignées sur une ligne de ventilation réelle.
--
-- Objectif : permettre à l'écran Factures d'afficher les montants Sage bruts (non fiables, non
-- déclarables) d'une pièce exclue, pour investigation manuelle côté ERP (comparer aux montants
-- Sage, identifier la pièce fautive) — sans jamais relire Sage à la demande depuis l'écran
-- (capture faite une fois, au moment de la détection de l'incohérence, cf. OrchestrateurDeclaration
-- / SageTaxReaderService).
--
-- L'identification de la pièce (N°, date, tiers) n'a pas besoin d'une colonne dédiée : elle est
-- déjà portée par la famille A (RT_ECHEANCE) de FactureInterrogationRow, disponible pour TOUTE
-- ligne (y compris exclue), indépendamment du cache de ventilation.
--
-- Idempotent : n'ajoute chaque colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutHT'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ADD [BrutHT] DECIMAL(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutTva'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ADD [BrutTva] DECIMAL(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutParafiscale'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ADD [BrutParafiscale] DECIMAL(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutTtc'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ADD [BrutTtc] DECIMAL(18,4) NULL;
END;
GO
