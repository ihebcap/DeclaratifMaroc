using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Tiers
{
	public int TiersNo { get; set; }

	public TiersType Type { get; set; }

	public int DelaiReg { get; set; }

	public int SocieteNo { get; set; }

	public string Numero { get; set; }

	public string Intitule { get; set; }

	public NiveauModeReg NiveauReg { get; set; }

	public bool EnSommeil { get; set; }
}
