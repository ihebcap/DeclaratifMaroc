using System;

namespace Tresorerie.Core.Models;

public class Garantie
{
	public int No { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }
}
