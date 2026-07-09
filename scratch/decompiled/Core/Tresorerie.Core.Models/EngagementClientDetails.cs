using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class EngagementClientDetails
{
	public int No { get; set; }

	public string Numero { get; set; }

	public string Reference { get; set; }

	public DateTime Date { get; set; }

	public DateTime Echeance { get; set; }

	public DateTime EcheanceMonth => new DateTime(Echeance.Year, Echeance.Month, DateTime.DaysInMonth(Echeance.Year, Echeance.Month));

	public decimal Montant { get; set; }

	public TypeEngagementClient Type { get; set; }

	public string TypeDescription => Type.GetDisplayDescription();

	public int ClientNo { get; set; }

	public string ClientNumero { get; set; }

	public string ClientIntitule { get; set; }

	public int SocieteNo { get; set; }
}
