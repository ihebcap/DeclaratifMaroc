using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Mouvement
{
	public DateTime Date { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public string Libelle { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Tire { get; set; }

	public string Piece { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersNumero { get; set; }

	public int? TiersNo { get; set; }

	public ReglementType Type { get; set; }

	public bool IsPointe { get; set; }

	public Mouvement()
	{
	}

	public Mouvement(int no, string numero)
	{
		No = no;
		Numero = numero;
	}
}
