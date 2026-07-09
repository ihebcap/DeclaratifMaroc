using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class OperationBancaire : ICanBeComptabilise
{
	public int BanqueNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DatePointe { get; set; }

	public bool IsPointe { get; set; }

	public string Libelle { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantTva { get; set; }

	public int No { get; set; }

	public string Piece { get; set; }

	public SensPrevisionnelle Sens { get; set; }

	public int SocieteNo { get; set; }

	public string TypeCode { get; set; }

	public int TypeNo { get; set; }

	public bool IsComptabilise { get; set; }

	public DateTime DateComptabilisation { get; set; }

	public int? DeclarationTvaEncaissementNo { get; set; }

	public string DossierNumero { get; set; }

	public string AffaireNumero { get; set; }

	public int? BordereauNo { get; set; }

	public string BordereauNumero { get; set; }
}
