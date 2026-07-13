-- TASK-024 : Cache des ventilations Sage (EC_Type = 0)
-- Clé : (EC_Id, Taux) — une ligne par bucket TVA par facture.
-- Token de paiement : MV_Id + MV_Point capturés au moment de l'écriture.
-- Validation à la lecture : requête locale RT_AFFECTATION/RT_MOUVEMENT.
-- Ne jamais écrire dans la base GRFN. Cible : base dédiée (PersistenceConnection).

CREATE TABLE [GRC_VENTILATION_SAGE_CACHE] (
    -- Clé primaire composite : facture + bucket taux
    [EC_Id]          INT             NOT NULL,
    [Taux]           DECIMAL(18,4)   NOT NULL,

    -- Ventilation de la facture pour ce taux
    [BaseHT]         DECIMAL(18,4)   NOT NULL,
    [MontantTva]     DECIMAL(18,4)   NOT NULL,
    [TTC]            DECIMAL(18,4)   NOT NULL,
    [CodeTaxe]       NVARCHAR(50)    NOT NULL DEFAULT '',

    -- Totaux de la facture (redondant pour validation rapide sans joins)
    [TotalHT]        DECIMAL(18,4)   NOT NULL,
    [TotalTva]       DECIMAL(18,4)   NOT NULL,
    [TotalTtc]       DECIMAL(18,4)   NOT NULL,

    -- Token de paiement capturé à l'écriture.
    -- NULL = facture lue mais non rattachée à un règlement pointé : ventilation
    -- « brute » conservée (réutilisable pour l'affichage) mais non déclarable.
    [Token_MV_Id]    INT             NULL,
    [Token_MV_Point] INT             NULL,

    -- Audit
    [DateLecture]    DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [Source]         NVARCHAR(100)   NOT NULL DEFAULT 'OM',

    CONSTRAINT [PK_GRC_VENTILATION_SAGE_CACHE] PRIMARY KEY ([EC_Id], [Taux])
);

-- Index pour lookup rapide par EC_Id
CREATE INDEX [IX_GRC_VENTILATION_SAGE_CACHE_ECId]
ON [GRC_VENTILATION_SAGE_CACHE] ([EC_Id]);
