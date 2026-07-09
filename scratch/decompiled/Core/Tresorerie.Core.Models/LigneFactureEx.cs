using System;

namespace Tresorerie.Core.Models;

public class LigneFactureEx : LigneFacture
{
	public string CompteGeneralArticle { get; set; }

	public LigneFactureEx(LigneFacture ligne, string compteGeneralArticle)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		base.No = ligne.No;
		base.FactureNo = ligne.FactureNo;
		base.Article = ligne.Article;
		base.Designation = ligne.Designation;
		base.Quantite = ligne.Quantite;
		base.PrixUnitaire = ligne.PrixUnitaire;
		base.PrixUnitaireDevise = ligne.PrixUnitaireDevise;
		base.Remise = ligne.Remise;
		base.MontantHT = ligne.MontantHT;
		base.MontantTTC = ligne.MontantTTC;
		base.Taxe1 = ligne.Taxe1;
		base.Taxe2 = ligne.Taxe2;
		base.Taxe3 = ligne.Taxe3;
		CompteGeneralArticle = compteGeneralArticle;
	}
}
