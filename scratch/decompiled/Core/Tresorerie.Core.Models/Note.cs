using System;

namespace Tresorerie.Core.Models;

public class Note
{
	public DateTime Date { get; set; }

	public int EntiteNo { get; set; }

	public TypeEntity EntiteType { get; set; }

	public string EntiteNumero { get; set; }

	public bool HasPieceJointe { get; set; }

	public int No { get; set; }

	public string NomPieceJointe { get; set; }

	public byte[] PieceJointe { get; set; }

	public int SocieteNo { get; set; }

	public string Text { get; set; }

	public int UtilisateurNo { get; set; }
}
