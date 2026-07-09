using System;

namespace Tresorerie.Core.Models;

public class AttestationRetenueTiers
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int TiersNo { get; set; }

	public string TiersCode { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateDebut { get; set; }

	public DateTime DateFin { get; set; }

	public string FileName { get; set; }

	public string CodeVerification { get; set; }

	public bool IsValide
	{
		get
		{
			if (IsEnRegle)
			{
				return DateFin.Date >= DateTime.Now.Date;
			}
			return false;
		}
	}

	public byte[] FilePdf { get; set; }

	public int Annee { get; set; }

	public bool IsEnRegle { get; set; }
}
