using System;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class LigneControleDelaisPaiementFiltreView
{
	public DateTime DateDu { get; set; }

	public DateTime DateAu { get; set; }

	public LigneControleDelaisPaiementFiltreView()
	{
		DateDu = DateTime.Now.AddDays(-90.0).Date;
		DateAu = DateTime.Now.Date;
	}
}
