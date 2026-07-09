using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum Remplace : short
{
	[Description("Non remplacé")]
	NonRemplace,
	[Description("Partiellement remplacé")]
	PartiellementRemplace,
	[Description("Totalement remplacé")]
	TotalementRemplace
}
