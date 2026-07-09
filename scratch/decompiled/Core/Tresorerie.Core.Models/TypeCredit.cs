using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class TypeCredit
{
	public int No { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public MethodeCalculTypeCredit MethodeCalcul { get; set; }

	public ConventionCalculTypeCredit ConventionCalcul { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }

	public NatureTypeCredit Nature { get; set; }
}
