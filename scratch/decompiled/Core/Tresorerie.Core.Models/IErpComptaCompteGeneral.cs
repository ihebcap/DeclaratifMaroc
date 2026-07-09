using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpComptaCompteGeneral
{
	string Abrege { get; }

	int DeviseNo { get; }

	string Intitule { get; set; }

	bool IsEnSommeil { get; }

	bool IsLettrageAuto { get; }

	bool IsSaisieDevise { get; }

	bool IsSaisieMode { get; }

	bool IsSaisieTiers { get; }

	ErpComptaNatureCompteGeneral Nature { get; }

	int No { get; }

	string Numero { get; }

	ErpComptaTypeReport Report { get; }

	ErpComptaTypeCompteGeneral TypeNo { get; }
}
