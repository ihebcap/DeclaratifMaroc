using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class MouvementBancaire
{
	public int BanqueNo { get; set; }

	public int BordereauNo { get; set; }

	public string BordereauNumero { get; set; }

	public DateTime Date { get; set; }

	public DateTime DatePointage { get; set; }

	public DateTime DateRemis { get; set; }

	public DateTime DateValeurPrevu { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public DateTime Echeance { get; set; }

	public bool IsPointe { get; set; }

	public EtatPreavis Preavis { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int DeviseNo { get; set; }

	public TypeMouvementBancaire MvtType { get; set; }

	public NatureTypeBordereau? Nature { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public int Remis { get; set; }

	public SensMouvementBancaire Sens { get; set; }

	public int SocieteNo { get; set; }

	public int TiersNo { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public EtatComptabilite IsComptabilise { get; set; }

	public string InfoLibre1 { get; set; }

	public string InfoLibre2 { get; set; }

	public string InfoLibre3 { get; set; }

	public string InfoLibre4 { get; set; }
}
