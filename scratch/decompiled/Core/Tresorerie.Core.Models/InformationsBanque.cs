using System;
using System.ComponentModel;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class InformationsBanque
{
	public int BanqueNo { get; set; }

	public DateTime DateSolde { get; set; } = new DateTime(1900, 1, 1);

	public bool DefaultBanque { get; set; }

	public decimal EncoursEscompteAutorise { get; set; }

	public int IsComptaDetail { get; set; }

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
		set
		{
			IsEcritureCumule = value == TypeGroupementEcriture.Cumule;
		}
	}

	public int NbrJoursCouverture { get; set; }

	public int No { get; set; }

	public int SocieteNo { get; set; }

	public decimal SoldeInitial { get; set; }

	public BindingList<InformationsBanqueBordereau> Bordereaux { get; set; } = new BindingList<InformationsBanqueBordereau>();

	public bool EnSommeil { get; set; }

	public bool IsComptaFraisCumule { get; set; }

	public string CodeRegroupement { get; set; }

	public string AdresseCompte { get; set; }

	public decimal FaciliteCaisse { get; set; }

	public string BanqueIce { get; set; }

	public string BanqueIdentifiant { get; set; }

	public BindingList<StructureVirement> StructureBordereauxVirementTxt { get; set; } = new BindingList<StructureVirement>();

	public BindingList<StructureVirement> StructureBordereauxVirementXml { get; set; } = new BindingList<StructureVirement>();

	public BindingList<StructureVirement> StructureBordereauxVirementExcel { get; set; } = new BindingList<StructureVirement>();

	public string PrefixEntete { get; set; }

	public string PrefixDetail { get; set; }

	public string PrefixPied { get; set; }

	public TypeFichierBordoreau ExtensionFichier { get; set; }

	public int TailleEntete { get; set; }

	public int TailleDetail { get; set; }

	public int TaillePied { get; set; }

	public bool IsUtiliseOrdreVirement { get; set; }

	public string OrderVirementCompteGeneral { get; set; }

	public string OrderVirementJournal { get; set; }

	public DateTime DateQuota { get; set; }

	public decimal Quota { get; set; }
}
