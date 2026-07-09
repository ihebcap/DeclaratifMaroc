using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum DesignationDocumentNatureOperation
{
	[Description("Achat de biens d’équipement")]
	AchatBiensEquipement = 1,
	[Description("Achat de travaux")]
	AchatTravaux,
	[Description("Achat de services")]
	AchatService
}
