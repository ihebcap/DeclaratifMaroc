using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RegleRetenueSourceTva
{
	public int No { get; set; }

	public string Nom { get; set; }

	public int DesignationDocumentNo { get; set; }

	public NatureFournisseur NatureTiers { get; set; }

	public bool HasAttestation { get; set; }

	public RegleRetenueTvaCondition Condition { get; set; }

	public decimal Montant { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal FacturationMensuelle { get; set; }

	public string DesignationDocuementValuesView { get; set; }

	public List<int> DesignationDocumentValueViewNo
	{
		get
		{
			if (!(DesignationDocuementValuesView == string.Empty) && DesignationDocuementValuesView != null)
			{
				return DesignationDocuementValuesView.ToString().Split(',').Select(int.Parse)
					.ToList();
			}
			return new List<int>();
		}
	}
}
