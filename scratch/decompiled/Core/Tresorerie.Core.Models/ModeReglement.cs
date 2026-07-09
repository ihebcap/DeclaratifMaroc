using Tresorerie.Core.Enum;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class ModeReglement
{
	public string AnnexeCode { get; set; }

	public int? AnnexeType { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public bool IsRetenu { get; set; }

	public bool IsTransferable { get; set; }

	public int No { get; protected set; }

	public decimal Taux { get; set; }

	public ReglementType Type { get; set; }

	public bool IsModeAvoir { get; set; }

	public bool EnSommeil { get; set; }

	public bool IsInclusCalculPrime { get; set; }

	public bool IsRetenuGarantie { get; set; }

	public bool IsComptaTiersToBanque { get; set; }

	public bool IsReferenceReglementClientObligatoire { get; set; }

	public bool ControllerUniciteReferenceReglementClient { get; set; }

	public bool SoumisDroitTimbre { get; set; }

	public ModeReglement()
	{
		Intitule = string.Empty;
		Code = string.Empty;
		Type = ReglementType.Espece;
	}

	public ModeReglement(int id, string code, ReglementType type, bool isRetenue, bool isTransferable, int? annexeType, string annexeCode, decimal taux, bool isModeAvoir, bool enSommeil)
		: this()
	{
		No = id;
		Code = code;
		Type = type;
		IsRetenu = isRetenue;
		IsTransferable = isTransferable;
		AnnexeType = annexeType;
		AnnexeCode = annexeCode;
		Taux = taux;
		IsModeAvoir = isModeAvoir;
		EnSommeil = enSommeil;
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoModeReglement, No, Code);
	}
}
