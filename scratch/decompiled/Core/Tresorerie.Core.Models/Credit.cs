using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Credit
{
	public int No { get; set; }

	public string Numero { get; set; }

	public string Intitule { get; set; }

	public DateTime Date { get; set; }

	public int BanqueNo { get; set; }

	public decimal Montant { get; set; }

	public int NbMois { get; set; }

	public DateTime FirstEcheance { get; set; }

	public decimal TauxInterret { get; set; }

	public int TypeNo { get; set; }

	public bool IsComptabiliser { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }

	public MethodeCalculTypeCredit MethodeCalcul { get; set; }

	public ConventionCalculTypeCredit ConventionCalcul { get; set; }

	public StatutCredit Statut { get; set; }

	public decimal FraisDossier { get; set; }

	public decimal MontantAssurence { get; set; }

	public bool IsAssuranceVentiler { get; set; }

	public bool IsFraisDossierVentiler { get; set; }

	public TypeDatePrelevelmentCredit TypeDatePrelevement { get; set; }

	public int? TiersNo { get; set; }

	public int? DocumentNo { get; set; }

	public int DeviseNo { get; set; }

	public decimal CoursDevise { get; set; }

	public int? ReglementNo { get; set; }

	public string ReglementNum { get; set; }

	public bool IsInclureTresoPrev { get; set; }

	public DateTime DateDeblocage { get; set; }

	public string AffaireNumero { get; set; }
}
