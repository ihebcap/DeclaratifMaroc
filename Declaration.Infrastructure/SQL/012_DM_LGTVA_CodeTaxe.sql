-- TASK-198 (gap comble) : la colonne CodeTaxe (F_TAXE.TA_Code) etait deja propagee jusqu'a
-- ConstructeurDeclaration/LigneCandidate/DTOs/Excel/front, mais jamais persistee sur DM_LGTVA --
-- SaveLignesCandidatesAsync ne l'ecrivait pas dans l'INSERT, donc GetLignesAsync (SELECT *) la
-- relisait toujours vide. Meme trou, meme cause, que TASK-189/Reference (011_DM_LGTVA_Reference.sql).
--
-- NVARCHAR(50) NULL : meme convention que les autres colonnes non-financieres de cette table
-- (NumeroFacture/TiersNom/ModePaiement/Source) -- absence de valeur deja geree "" cote
-- consommateurs (MapLignesCandidates/ConstruireModele*Async), jamais une exception Dapper sur une
-- colonne string.
--
-- Idempotent : n'ajoute la colonne que si elle est absente. Additive et nullable : aucune perte de
-- donnee sur les lignes DM_LGTVA deja persistees (elles restent CodeTaxe = NULL tant qu'elles ne
-- sont pas refigees -- meme reserve documentee que TASK-186/187/189 sur les declarations deja closes).
-- Cible : base de persistance dediee (PersistenceConnection), table DM_* possedee par GRF -- jamais
-- une table apbs-gr_winform.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'CodeTaxe'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [CodeTaxe] NVARCHAR(50) NULL;
END;
GO
