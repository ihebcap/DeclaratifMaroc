using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.views;

public class LigneDeclarationRetenuSourceView_
{
	public int No { get; set; }

	public string Numero { get; set; }

	public int CaisseNo { get; set; }

	public TiersType TiersType { get; set; }

	public DateTime Date { get; set; }

	public string Beneficiaire { get; set; }

	public int FournisseurNo { get; set; }

	public string FournisseurCode { get; set; }

	public string FournisseurIntitule { get; set; }

	public string Libelle { get; set; }

	public decimal Base { get; set; }

	public decimal Taux { get; set; }

	public int DeviseNo { get; set; }

	public int? DossierNo { get; set; }

	public int ModeNo { get; set; }

	public string ModeReg { get; set; }

	public decimal Montant { get; set; }

	public int DeclarationNo { get; set; }

	public string DeclarationNum { get; set; } = string.Empty;

	public bool IsDeclarer
	{
		get
		{
			if (string.IsNullOrEmpty(DeclarationNum))
			{
				return DeclarationNo > 0;
			}
			return true;
		}
	}

	public int SocieteNo { get; set; }
}
