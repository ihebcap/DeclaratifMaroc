-- Solde initial GRF (RT_ECHEANCE.EC_Type = 4, cf. DONE_DETAIL/TASK-025) : le montant du solde n'a
-- aucun détail HT/TVA/taux côté Sage (connu en TTC seul). Décision PO : intégrer ces lignes à la
-- déclaration TVA moyennant une saisie MANUELLE du comptable (taux + montant de TVA), plutôt que
-- les éliminer silencieusement comme auparavant. Cette table persiste cette saisie, par société et
-- par échéance (EC_Id) — l'orchestrateur la recharge en batch avant de construire la déclaration
-- (OrchestrateurDeclaration.Traiter).
--
-- Idempotent (CREATE TABLE IF NOT EXISTS). Cible : base de persistance dédiée
-- (PersistenceConnection, même base que DM_LGTVA/DM_VENTILATION_SAGE_CACHE). Jamais GRFN.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DM_SOLDE_INITIAL_TVA'
)
BEGIN
    CREATE TABLE [dbo].[DM_SOLDE_INITIAL_TVA](
        [SO_Id]       INT             NOT NULL,
        [EC_Id]       INT             NOT NULL,
        [Taux]        DECIMAL(18, 6)  NOT NULL,
        [MontantTva]  DECIMAL(18, 6)  NOT NULL,
        [SaisiPar]    NVARCHAR(200)   NULL,
        [SaisiLe]     DATETIME        NOT NULL,
        CONSTRAINT [PK_DM_SOLDE_INITIAL_TVA] PRIMARY KEY CLUSTERED ([SO_Id] ASC, [EC_Id] ASC)
    ) ON [PRIMARY];
END;
GO
