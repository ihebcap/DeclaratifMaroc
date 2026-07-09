using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Rappel
{
	public string ClientIntitule { get; set; }

	public int ClientNo { get; set; }

	public string ClientNumero { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCloture { get; set; }

	public DateTime DateCreation { get; set; }

	public DomaineRappel Domaine { get; set; }

	public bool IsCloture { get; set; }

	public bool IsRelance { get; set; }

	public int No { get; set; }

	public string Note { get; set; }

	public string Numero { get; set; }

	public int? ParentNo { get; set; }

	public string ParentNumero { get; set; }

	public int SocieteNo { get; set; }

	public decimal SoldeActuel { get; set; }

	public decimal SoldeRappel { get; set; }

	public int UtilisateurNo { get; set; }
}
