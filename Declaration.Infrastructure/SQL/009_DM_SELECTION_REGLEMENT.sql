-- TASK-097 : Table de persistance de la selection des reglements
-- Clic « Passer au calcul » transmet et persiste cette selection.
-- Un retour sur la declaration la recharge pour reproposer la selection telle que laissee.
-- Idempotent : ne cree la table que si elle est absente.
-- Cible : base de persistance dediee (PersistenceConnection).

IF OBJECT_ID('dbo.DM_SELECTION_REGLEMENT', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DM_SELECTION_REGLEMENT (
        DeclarationId    NVARCHAR(36)  NOT NULL,
        NumeroReglement  NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_DM_SELECTION_REGLEMENT PRIMARY KEY (DeclarationId, NumeroReglement),
        CONSTRAINT FK_DM_SELECTION_REGLEMENT_DM_ENTTVA 
            FOREIGN KEY (DeclarationId) REFERENCES dbo.DM_ENTTVA(Id) ON DELETE CASCADE
    );
END;
GO
