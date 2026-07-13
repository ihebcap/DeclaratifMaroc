-- TASK-072 : colonne du motif d'erreur, portée par une ligne sentinelle unique
-- (Taux=-1, CodeTaxe='ERREUR') quand une pièce est exclue de la valorisation
-- (incohérence Sage HT+TVA≠TTC, ou TTC Sage≠RT_ECHEANCE.EC_MtDevise). Jamais
-- renseignée sur une ligne de ventilation réelle.
--
-- Rend le motif exact exploitable par l'écran Factures (GET /factures, famille B)
-- sans avoir à relire l'OM Sage à chaque rafraîchissement.
--
-- Idempotent : n'ajoute la colonne que si elle est absente.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE'
      AND COLUMN_NAME = 'MotifErreur'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ADD [MotifErreur] NVARCHAR(500) NULL;
END;
