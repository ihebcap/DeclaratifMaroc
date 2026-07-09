using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class HistoriqueMvt
{
	public int CaisseNo { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public Lot Lot { get; set; }

	public int ModeReglementNo { get; set; }

	public int DeviseNo { get; set; }

	public decimal Montant => Lot.Montant;

	public int MouvementInNo { get; set; }

	public int MouvementOutNo { get; set; }

	public int No { get; set; }

	public SensMouvement Sens { get; set; }

	public StatutTransfert Statut { get; set; }

	public string Collaborateur { get; set; }

	public HistoriqueMvt(int no, int caisseNo, SensMouvement sens, MouvementDomaine domaine, int modeReglementNo, int mouvementInNo, int mouvementOutNo, StatutTransfert statut, int deviseNo, Lot lot)
	{
		No = no;
		CaisseNo = caisseNo;
		Sens = sens;
		Domaine = domaine;
		ModeReglementNo = modeReglementNo;
		MouvementInNo = mouvementInNo;
		MouvementOutNo = mouvementOutNo;
		Lot = lot;
		Statut = statut;
		DeviseNo = deviseNo;
	}
}
