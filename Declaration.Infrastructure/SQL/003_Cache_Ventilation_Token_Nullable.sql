-- Migration : rendre le token de paiement NULLable dans le cache des ventilations.
--
-- Motif : on met désormais en cache TOUTES les factures lues en OM, même sans
-- règlement pointé (lecture OM propre à la facture, indépendante du paiement).
-- Une ligne à token NULL est conservée (réutilisable pour l'affichage) mais
-- n'est jamais servie comme ventilation déclarable tant qu'un token réel n'existe pas.
--
-- Idempotent : ne modifie les colonnes que si elles sont encore NOT NULL.
-- Cible : base de persistance dédiée (PersistenceConnection). Jamais GRFN.

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE'
      AND COLUMN_NAME = 'Token_MV_Id'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ALTER COLUMN [Token_MV_Id] INT NULL;
END;

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GRC_VENTILATION_SAGE_CACHE'
      AND COLUMN_NAME = 'Token_MV_Point'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    ALTER TABLE [GRC_VENTILATION_SAGE_CACHE] ALTER COLUMN [Token_MV_Point] INT NULL;
END;
