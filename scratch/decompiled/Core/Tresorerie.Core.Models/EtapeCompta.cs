using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class EtapeCompta
{
	public string CompteGeneral { get; set; }

	public string Journal { get; set; }

	public NatureActionEtape NatureAction { get; set; }

	public NatureDebitEtape NatureDebit { get; set; }

	public NatureJournalEtape NatureJournal { get; set; }

	public int No { get; set; }

	public DateComptaEtape OptionDate { get; set; }

	public int Order { get; set; }

	public int SocieteNo { get; set; }

	public int TypeBordereauNo { get; set; }
}
