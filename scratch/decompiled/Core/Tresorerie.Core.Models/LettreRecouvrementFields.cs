using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LettreRecouvrementFields
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public LettreRecouvrementFieldValue Field { get; set; }

	public bool IsGenere { get; set; }

	public int Rang { get; set; }
}
