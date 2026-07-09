using System;

namespace Tresorerie.Core.Models;

public class ConventionDelaisPaiementTiers
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int TiersNo { get; set; }

	public string TiersCode { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public DateTime? DateDebut { get; set; }

	public DateTime? DateFin { get; set; }

	public string FileName { get; set; }

	public int NombreJoursDelaisPaiement { get; set; }

	public int? FactureNo { get; set; }

	public string FactureNumero { get; set; }

	public DomaineConvention Domaine { get; set; }

	public TypeConvention Type { get; set; }

	public byte[] FilePdf { get; set; }
}
