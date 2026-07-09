using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RetenuALaSource : ICanBeComptabilise
{
	public string AnnexeCode { get; set; }

	public int AnnexeType { get; set; }

	public decimal Base { get; set; }

	public string Beneficiaire { get; set; }

	public int CaisseNo { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateImprime { get; set; }

	public DateTime DateRecuperation { get; set; }

	public int DeviseNo { get; set; }

	public int? DossierNo { get; set; }

	public string DossierNumero { get; set; }

	public string FournisseurCode { get; set; }

	public string FournisseurIntitule { get; set; }

	public TiersType TiersType { get; set; }

	public int FournisseurNo { get; set; }

	public bool IsComptabilise { get; set; }

	public bool IsImprime { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public string ModeReg { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal Solde { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public decimal SoldeDeviseSociete { get; set; }

	public decimal MtTimbre { get; set; }

	public int NbTimbre { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string RecouvreurCin { get; set; }

	public string RecouvreurNom { get; set; }

	public RemisFournisseur Recuperer { get; set; }

	public int SocieteNo { get; set; }

	public decimal Taux { get; set; }

	public int UtilisateurNo { get; set; }

	public bool IsReserveDossierFrs { get; set; }

	public string AffaireNumero { get; set; }

	public int? RetenuePourEcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public int? DeclarationNo { get; set; }

	public string DeclarationNum { get; set; } = string.Empty;

	public bool IsDeclarer
	{
		get
		{
			if (string.IsNullOrEmpty(DeclarationNum))
			{
				if (DeclarationNo is int)
				{
					return DeclarationNo > 0;
				}
				return false;
			}
			return true;
		}
	}

	public decimal MontantHorsTaxe { get; set; }

	public decimal TauxTva { get; set; }

	public decimal MontantTva { get; set; }

	public int AnneeFacturation { get; set; }

	public bool IsPrisCharge { get; set; }

	public bool IsConvention { get; set; }

	public int? OperationRetenueNo { get; set; }

	public decimal MontantNetPaye => Base - Montant;

	public string InfoLibre1 { get; set; }

	public string InfoLibre2 { get; set; }

	public string InfoLibre3 { get; set; }

	public string InfoLibre4 { get; set; }
}
