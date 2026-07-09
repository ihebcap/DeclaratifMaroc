using System;
using Tresorerie.Infrastructure;

namespace Tresorerie.Core.Models;

public class Subscriber
{
	public DateTime Date { get; set; }

	public Guid Id { get; set; }

	public byte[] Rowversion { get; set; }

	public int SocieteNo { get; set; }

	public string UtilisateurLogin { get; set; }

	public string UtilisateurPoste { get; set; }

	public int UtilisateurNo { get; set; }

	public ApplicationRunning ApplicationRunning { get; set; }
}
