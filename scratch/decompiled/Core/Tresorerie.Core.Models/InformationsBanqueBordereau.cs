using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class InformationsBanqueBordereau
{
	public int No { get; set; }

	public int InformationsBanqueNo { get; set; }

	public int TypeBordereauNo { get; set; }

	public int OperationBancaire { get; set; }

	public bool IsEcritureCumule { get; set; }

	public TypeGroupementEcriture GroupementEcriture
	{
		get
		{
			if (IsEcritureCumule)
			{
				return TypeGroupementEcriture.Cumule;
			}
			return TypeGroupementEcriture.Detaille;
		}
	}
}
