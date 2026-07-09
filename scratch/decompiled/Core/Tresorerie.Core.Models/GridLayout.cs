using System;

namespace Tresorerie.Core.Models;

public class GridLayout
{
	public Guid Guid { get; set; }

	public byte[] Layout { get; set; }

	public string Name { get; set; }

	public int No { get; set; }

	public int UtilisateurNo { get; set; }
}
