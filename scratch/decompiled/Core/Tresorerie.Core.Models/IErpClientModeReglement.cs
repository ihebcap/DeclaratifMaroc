namespace Tresorerie.Core.Models;

public interface IErpClientModeReglement
{
	string Condition { get; set; }

	int JourTb01 { get; set; }

	int JourTb02 { get; set; }

	int JourTb03 { get; set; }

	int JourTb04 { get; set; }

	int JourTb05 { get; set; }

	int JourTb06 { get; set; }

	string JourTomber { get; set; }

	string ModeleReglement { get; set; }

	string ModeReglement { get; set; }

	int NbJour { get; set; }

	int No { get; set; }

	string Num { get; set; }

	int Repart { get; set; }

	string Valeur { get; set; }
}
