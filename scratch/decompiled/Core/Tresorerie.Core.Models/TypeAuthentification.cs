using System.ComponentModel;

namespace Tresorerie.Core.Models;

public enum TypeAuthentification
{
	[Description("Authentification Windows")]
	Windows,
	[Description("Authentification Sql")]
	Sql
}
