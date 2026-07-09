using System;

namespace Tresorerie.Core.Models;

public class CaisseModeReglement : ModeReglement
{
	public int CaisseNo { get; private set; }

	public string CompteGeneralDecaissement { get; set; }

	public string CompteGeneralEncaissement { get; set; }

	public string CompteGeneralImpayeEncaissement { get; set; }

	public string CompteGeneralImpayeDecaissement { get; set; }

	public string JournalDecaissement { get; set; }

	public string JournalEncaissement { get; set; }

	public int SocieteNo { get; private set; }

	public string CompteGeneralAvanceEncaissement { get; set; }

	public string CompteGeneralAvanceDecaissement { get; set; }

	public CaisseModeReglement(ModeReglement mode, int caisseNo, int societeNo)
		: base(mode.No, mode.Code, mode.Type, mode.IsRetenu, mode.IsTransferable, mode.AnnexeType.GetValueOrDefault(), mode.AnnexeCode, mode.Taux, mode.IsModeAvoir, mode.EnSommeil)
	{
		if (mode == null)
		{
			throw new ArgumentNullException("mode");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentNullException("caisseNo");
		}
		CaisseNo = caisseNo;
		base.Intitule = mode.Intitule;
		base.SoumisDroitTimbre = mode.SoumisDroitTimbre;
		JournalEncaissement = string.Empty;
		CompteGeneralEncaissement = string.Empty;
		JournalDecaissement = string.Empty;
		CompteGeneralDecaissement = string.Empty;
		CompteGeneralImpayeEncaissement = string.Empty;
		CompteGeneralImpayeDecaissement = string.Empty;
		CompteGeneralAvanceEncaissement = string.Empty;
		SocieteNo = societeNo;
	}
}
