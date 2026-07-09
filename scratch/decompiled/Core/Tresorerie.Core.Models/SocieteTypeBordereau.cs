using System.Collections.Generic;

namespace Tresorerie.Core.Models;

public class SocieteTypeBordereau : TypeBordereau
{
	public List<EtapeCompta> Etapes { get; set; }

	public int SocieteNo { get; set; }

	public SocieteTypeBordereau(TypeBordereau type, int societeNo)
		: base(type.No, type.Code, type.GetModeReglements)
	{
		SocieteNo = societeNo;
		base.Intitule = type.Intitule;
		Etapes = new List<EtapeCompta>();
	}
}
