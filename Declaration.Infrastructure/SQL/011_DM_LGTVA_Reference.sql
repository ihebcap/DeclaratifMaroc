-- TASK-189 : comble le trou laisse par TASK-187 -- la colonne Reference (RT_ECHEANCE.DO_Reference)
-- etait propagee jusqu'a ConstructeurDeclaration/Exporter.cs mais jamais persistee sur DM_LGTVA, donc
-- jamais lue par les deux methodes reellement cablees cote API (ConstruireModeleExportAsync/
-- ConstruireModeleControleAsync, qui lisent LigneCandidate/DM_LGTVA via GetLignesAsync, jamais
-- ConstructeurDeclaration). Voir DONE_DETAIL/TASK-187_verify.md pour le detail du gap.
--
-- NVARCHAR(200) : source RT_ECHEANCE.DO_Reference est NVARCHAR(MAX), mais verification reelle sur
-- GR_EMA_DISTRIBUTION (3811 lignes) donne une longueur max observee de 25 caracteres -- 200 laisse une
-- marge large sans reprendre le NVARCHAR(MAX) source, coherent avec la convention deja en place sur
-- cette table (MotifRejet NVARCHAR(500), TiersNom NVARCHAR(255)).
-- NULL (pas NOT NULL DEFAULT '') : meme convention que NumeroFacture/TiersNom/ModePaiement/Source sur
-- cette meme table -- absence de valeur deja geree "" cote consommateurs (MapLignesCandidates/
-- ConstruireModele*Async), jamais une exception Dapper sur une colonne string.
--
-- Idempotent : n'ajoute la colonne que si elle est absente. Additive et nullable : aucune perte de
-- donnee sur les lignes DM_LGTVA deja persistees (elles restent Reference = NULL tant qu'elles ne sont
-- pas refigees -- meme reserve documentee que TASK-186/187 sur les declarations deja closes).
-- Cible : base de persistance dediee (PersistenceConnection), table DM_* possedee par GRF -- jamais
-- une table apbs-gr_winform.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DM_LGTVA' AND COLUMN_NAME = 'Reference'
)
BEGIN
    ALTER TABLE [DM_LGTVA] ADD [Reference] NVARCHAR(200) NULL;
END;
GO
