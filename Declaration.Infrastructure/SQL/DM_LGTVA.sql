 

CREATE TABLE [dbo].[DM_LGTVA](
	[Id] [nvarchar](36) NOT NULL,
	[DeclarationId] [nvarchar](36) NULL,
	[Etat] [int] NULL,
	[Domaine] [nvarchar](50) NULL,
	[MotifRejet] [nvarchar](500) NULL,
	[NumeroFacture] [nvarchar](100) NULL,
	[TiersNom] [nvarchar](255) NULL,
	[TiersIdentifiantFiscal] [nvarchar](100) NULL,
	[TiersICE] [nvarchar](100) NULL,
	[HT] [decimal](18, 6) NULL,
	[Taux] [decimal](18, 6) NULL,
	[TVA] [decimal](18, 6) NULL,
	[TTC] [decimal](18, 6) NULL,
	[ModePaiement] [nvarchar](100) NULL,
	[DatePaiement] [datetime2](7) NULL,
	[DateFacture] [datetime2](7) NULL,
	[Source] [nvarchar](100) NULL,
	[NumeroRapprochement] [nvarchar](100) NULL,
	[EcType] [int] NOT NULL,
	[Prorata] [decimal](18, 6) NOT NULL,
	[MontantAffecte] [decimal](18, 6) NOT NULL,
	[EC_Id] [int] NOT NULL,
	[MV_Id] [int] NOT NULL,
	[IncoherenceValidee] [bit] NOT NULL,
	[IncoherenceValideePar] [nvarchar](200) NULL,
	[IncoherenceValideeLe] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  DEFAULT ((0)) FOR [EcType]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  CONSTRAINT [DF_LigneCandidate_Prorata]  DEFAULT ((0)) FOR [Prorata]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  CONSTRAINT [DF_LigneCandidate_MontantAffecte]  DEFAULT ((0)) FOR [MontantAffecte]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  DEFAULT ((0)) FOR [EC_Id]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  DEFAULT ((0)) FOR [MV_Id]
GO

ALTER TABLE [dbo].[DM_LGTVA] ADD  DEFAULT ((0)) FOR [IncoherenceValidee]
GO

ALTER TABLE [dbo].[DM_LGTVA]  WITH CHECK ADD  CONSTRAINT [FK_DM_LGTVA_DM_ENTTVA] FOREIGN KEY([DeclarationId])
REFERENCES [dbo].[DM_ENTTVA] ([Id])
GO

ALTER TABLE [dbo].[DM_LGTVA] CHECK CONSTRAINT [FK_DM_LGTVA_DM_ENTTVA]
GO


