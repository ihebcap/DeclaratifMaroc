using System;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class SocieteModeReglement : ModeReglement
{
	public int ErpNo { get; set; }

	public bool IsActive { get; set; }

	public int SocieteNo { get; private set; }

	public SocieteModeReglement(int societeNo, ModeReglement mode, int erpNo, bool isActive)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo, "societeNo");
		}
		if (mode == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorModeReglementInvalide, "ModeReglement");
		}
		base.Code = mode.Code;
		base.Type = mode.Type;
		base.Intitule = mode.Intitule;
		IsActive = isActive;
		base.IsTransferable = mode.IsTransferable;
		base.No = mode.No;
		SocieteNo = societeNo;
		ErpNo = erpNo;
		base.AnnexeType = mode.AnnexeType;
		base.IsRetenu = mode.IsRetenu;
		base.Taux = mode.Taux;
		base.EnSommeil = mode.EnSommeil;
		base.IsReferenceReglementClientObligatoire = mode.IsReferenceReglementClientObligatoire;
		base.ControllerUniciteReferenceReglementClient = mode.ControllerUniciteReferenceReglementClient;
		base.IsInclusCalculPrime = mode.IsInclusCalculPrime;
		base.SoumisDroitTimbre = mode.SoumisDroitTimbre;
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.IndoSocieteModeReglement, SocieteNo, base.No);
	}
}
