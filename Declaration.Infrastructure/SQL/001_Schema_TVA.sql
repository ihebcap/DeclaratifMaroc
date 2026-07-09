-- Script de création du schéma de persistance pour la déclaration TVA
-- Ne jamais exécuter sur la base GRFN. Cible : Base dédiée ou Schéma dédié.

CREATE TABLE [DeclarationEntete] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Numero] NVARCHAR(50) NOT NULL UNIQUE,
    [SocieteId] NVARCHAR(50) NOT NULL,
    [Exercice] INT NOT NULL,
    [Type] INT NOT NULL,
    [Periode] INT NOT NULL,
    [Statut] INT NOT NULL,
    [DateCreation] DATETIME2 NOT NULL,
    [DateCloture] DATETIME2 NULL
);

-- Contrainte d'unicité (Société, Exercice, Période, Type)
CREATE UNIQUE INDEX [IX_DeclarationEntete_Unicite] 
ON [DeclarationEntete] ([SocieteId], [Exercice], [Periode], [Type]);

CREATE TABLE [LigneCandidate] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [DeclarationId] UNIQUEIDENTIFIER NOT NULL,
    [Etat] INT NOT NULL,
    [Domaine] NVARCHAR(50) NOT NULL,
    [MotifRejet] NVARCHAR(MAX) NULL,
    [NumeroFacture] NVARCHAR(100) NULL,
    [TiersNom] NVARCHAR(200) NULL,
    [TiersIdentifiantFiscal] NVARCHAR(50) NULL,
    [TiersICE] NVARCHAR(50) NULL,
    [HT] DECIMAL(18,4) NOT NULL,
    [Taux] DECIMAL(18,4) NOT NULL,
    [TVA] DECIMAL(18,4) NOT NULL,
    [TTC] DECIMAL(18,4) NOT NULL,
    [ModePaiement] NVARCHAR(50) NULL,
    [DatePaiement] DATETIME2 NULL,
    [DateFacture] DATETIME2 NULL,
    [Source] NVARCHAR(50) NULL,
    
    CONSTRAINT [FK_LigneCandidate_Declaration] FOREIGN KEY ([DeclarationId]) 
    REFERENCES [DeclarationEntete] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_LigneCandidate_DeclarationId_Domaine] 
ON [LigneCandidate] ([DeclarationId], [Domaine]);
