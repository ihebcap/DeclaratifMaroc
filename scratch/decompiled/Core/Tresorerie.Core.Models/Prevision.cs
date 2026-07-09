using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Prevision
{
	public int BanqueNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DatePointe { get; set; }

	public DateTime Echeance { get; set; }

	public bool IsAnnuler { get; set; }

	public bool IsPointe { get; set; }

	public string Libelle { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public SensPrevisionnelle Sens { get; set; }

	public int SocieteNo { get; set; }

	public string TypeCode { get; set; }

	public int TypeNo { get; set; }

	public string AffaireNumero { get; set; }
}
