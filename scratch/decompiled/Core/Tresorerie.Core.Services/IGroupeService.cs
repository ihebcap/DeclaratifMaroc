using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public interface IGroupeService
{
	AnnexeManager AnnexeManager { get; }

	CodeActiviteManager CodeActiviteManager { get; }

	DesignationDocumentManager DesignationDocumentManager { get; }

	OperationRetenuSourceManager OperationRetenuSourceManager { get; }

	RegleRetenueSourceTvaManager RegleRetenueSourceTvaManager { get; }

	TypeBordereauxManager BordereauxTypeManager { get; }

	DepenseManager DepenseManager { get; }

	VentilationAnalytiqueManager VentilationAnalytiqueManager { get; }

	AlimentationCaisseManager AlimentationCaisseManager { get; }

	DeviseManager DeviseManager { get; }

	TypeErp Erp { get; set; }

	ModeReglementManager ModeReglementManager { get; }

	ProfilType ProfilType { get; set; }

	ProfilTypeEx ProfilTypeEx { get; set; }

	SocieteManager SocieteManager { get; }

	TypeCautionManager TypeCautionManager { get; }

	InformationBanqueManager InformationBanqueManager { get; }

	CreditManager CreditManager { get; }

	ExtraitBancaireManager ExtraitBancaireManager { get; }

	UtilisateurManager UtilisateurManager { get; }

	BanqueTiersManager BanqueTiersManager { get; }

	PrevisionnelManager PrevisionnelManager { get; }

	bool CheckLogin(string login, string password);

	IEnumerable<Subscriber> GetAllSession();

	DossierCommun GetDossierCommun();

	bool HasConnectedSession();

	void ThrowIfConnectedSession();

	void UpdateDossierCommun(string folderSurcharge);
}
