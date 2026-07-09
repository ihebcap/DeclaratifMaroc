using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum UserDefaultDashboard
{
	[Description("Aucun")]
	Aucun,
	[Description("Dashboard")]
	DashboardManager,
	[Description("Accueil")]
	Widget,
	[Description("Dashboard & Accueil")]
	DashboardAndWidget
}
