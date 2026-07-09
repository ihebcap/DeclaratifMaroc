using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpParamEncours
{
	ErpDeclenchementControlEncours DeclenchementControlEncours { get; }

	ErpBaseEncours ErpBaseEncours { get; }

	int No { get; }
}
