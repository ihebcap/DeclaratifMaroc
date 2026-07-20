 

CREATE TABLE [dbo].[DM_ENTTVA](
	[Id] [nvarchar](36) NOT NULL,
	[Numero] [nvarchar](50) NULL,
	[SocieteId] [int] NULL,
	[Exercice] [int] NULL,
	[Type] [int] NULL,
	[Periode] [int] NULL,
	[Statut] [int] NULL,
	[DateCreation] [datetime2](7) NULL,
	[DateCloture] [datetime2](7) NULL,
	[DT_Id] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO


