using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class GroupeService : IGroupeService
{
	private readonly AnnexeManager _annexeManager;

	private readonly TypeBordereauxManager _bordereauxTypeManager;

	private readonly DepenseManager _depenseManager;

	private readonly VentilationAnalytiqueManager _ventilationAnalytiqueManager;

	private readonly AlimentationCaisseManager _alimentationCaisseManager;

	private readonly DeviseManager _deviseManager;

	private readonly IDossierCommunRepository _dossierCommunRepository;

	private readonly ModeReglementManager _modeReglementManager;

	private readonly SocieteManager _societeManager;

	private readonly ISubscriberRepository _subscriberRepository;

	private readonly TypeCautionManager _typeCautionManager;

	private readonly UtilisateurManager _utilisateurManager;

	private readonly InformationBanqueManager _informationBanqueManager;

	private readonly ExtraitBancaireManager _extraitBancaireManager;

	private readonly BanqueTiersManager _banqueTiersManager;

	private readonly PrevisionnelManager _previsionnelManager;

	private readonly IPasswordHasher _passwordHasher;

	private readonly CodeActiviteManager _codeActiviteManager;

	private readonly DesignationDocumentManager _designationDocumentManager;

	private readonly RegleRetenueSourceTvaManager _regleRetenueSourceTvaManager;

	private readonly OperationRetenuSourceManager _operationRetenuSourceManager;

	private readonly CreditManager _creditManager;

	public AnnexeManager AnnexeManager
	{
		get
		{
			if (_annexeManager.Groupe == null)
			{
				_annexeManager.Groupe = this;
			}
			return _annexeManager;
		}
	}

	public CodeActiviteManager CodeActiviteManager
	{
		get
		{
			if (_codeActiviteManager.Groupe == null)
			{
				_codeActiviteManager.Groupe = this;
			}
			return _codeActiviteManager;
		}
	}

	public DesignationDocumentManager DesignationDocumentManager
	{
		get
		{
			if (_designationDocumentManager.Groupe == null)
			{
				_designationDocumentManager.Groupe = this;
			}
			return _designationDocumentManager;
		}
	}

	public OperationRetenuSourceManager OperationRetenuSourceManager
	{
		get
		{
			if (_operationRetenuSourceManager.Groupe == null)
			{
				_operationRetenuSourceManager.Groupe = this;
			}
			return _operationRetenuSourceManager;
		}
	}

	public CreditManager CreditManager
	{
		get
		{
			if (_creditManager.Groupe == null)
			{
				_creditManager.Groupe = this;
			}
			return _creditManager;
		}
	}

	public RegleRetenueSourceTvaManager RegleRetenueSourceTvaManager
	{
		get
		{
			if (_regleRetenueSourceTvaManager.Groupe == null)
			{
				_regleRetenueSourceTvaManager.Groupe = this;
			}
			return _regleRetenueSourceTvaManager;
		}
	}

	public TypeBordereauxManager BordereauxTypeManager
	{
		get
		{
			if (_bordereauxTypeManager.Groupe == null)
			{
				_bordereauxTypeManager.Groupe = this;
			}
			return _bordereauxTypeManager;
		}
	}

	public DepenseManager DepenseManager
	{
		get
		{
			if (_depenseManager.Groupe == null)
			{
				_depenseManager.Groupe = this;
			}
			return _depenseManager;
		}
	}

	public VentilationAnalytiqueManager VentilationAnalytiqueManager
	{
		get
		{
			if (_ventilationAnalytiqueManager.Groupe == null)
			{
				_ventilationAnalytiqueManager.Groupe = this;
			}
			return _ventilationAnalytiqueManager;
		}
	}

	public AlimentationCaisseManager AlimentationCaisseManager
	{
		get
		{
			if (_alimentationCaisseManager.Groupe == null)
			{
				_alimentationCaisseManager.Groupe = this;
			}
			return _alimentationCaisseManager;
		}
	}

	public DeviseManager DeviseManager
	{
		get
		{
			if (_deviseManager.Groupe == null)
			{
				_deviseManager.Groupe = this;
			}
			return _deviseManager;
		}
	}

	public TypeErp Erp { get; set; }

	public ModeReglementManager ModeReglementManager
	{
		get
		{
			if (_modeReglementManager.Groupe == null)
			{
				_modeReglementManager.Groupe = this;
			}
			return _modeReglementManager;
		}
	}

	public ProfilType ProfilType { get; set; }

	public ProfilTypeEx ProfilTypeEx { get; set; }

	public SocieteManager SocieteManager
	{
		get
		{
			if (_societeManager.Groupe == null)
			{
				_societeManager.Groupe = this;
			}
			return _societeManager;
		}
	}

	public TypeCautionManager TypeCautionManager
	{
		get
		{
			if (_typeCautionManager.Groupe == null)
			{
				_typeCautionManager.Groupe = this;
			}
			return _typeCautionManager;
		}
	}

	public InformationBanqueManager InformationBanqueManager
	{
		get
		{
			if (_informationBanqueManager.Groupe == null)
			{
				_informationBanqueManager.Groupe = this;
			}
			return _informationBanqueManager;
		}
	}

	public ExtraitBancaireManager ExtraitBancaireManager
	{
		get
		{
			if (_extraitBancaireManager.Groupe == null)
			{
				_extraitBancaireManager.Groupe = this;
			}
			return _extraitBancaireManager;
		}
	}

	public BanqueTiersManager BanqueTiersManager
	{
		get
		{
			if (_banqueTiersManager.Groupe == null)
			{
				_banqueTiersManager.Groupe = this;
			}
			return _banqueTiersManager;
		}
	}

	public PrevisionnelManager PrevisionnelManager
	{
		get
		{
			if (_previsionnelManager.Groupe == null)
			{
				_previsionnelManager.Groupe = this;
			}
			return _previsionnelManager;
		}
	}

	public UtilisateurManager UtilisateurManager
	{
		get
		{
			if (_utilisateurManager.Groupe == null)
			{
				_utilisateurManager.Groupe = this;
			}
			return _utilisateurManager;
		}
	}

	public GroupeService(SocieteManager societeManager, ModeReglementManager modeReglementManager, TypeBordereauxManager bordereauxTypeManager, DepenseManager depenseManager, VentilationAnalytiqueManager ventilationAnalytiqueManager, AlimentationCaisseManager alimentationCaisseManager, DeviseManager deviseManager, UtilisateurManager utilisateurManager, AnnexeManager annexeManager, IDossierCommunRepository dossierCommunRepository, ISubscriberRepository subscriberRepository, TypeCautionManager typeCautionManager, InformationBanqueManager informationBanqueManager, ExtraitBancaireManager extraitBancaireManager, BanqueTiersManager banqueTiersManager, PrevisionnelManager previsionnelManager, IPasswordHasher passwordHasher, CodeActiviteManager codeActiviteManager, DesignationDocumentManager designationDocumentManager, RegleRetenueSourceTvaManager regleRetenueSourceTvaManager, OperationRetenuSourceManager operationRetenuSourceManager, CreditManager creditManager)
	{
		_societeManager = societeManager ?? throw new ArgumentNullException("societeManager");
		_bordereauxTypeManager = bordereauxTypeManager ?? throw new ArgumentNullException("bordereauxTypeManager");
		_depenseManager = depenseManager ?? throw new ArgumentNullException("depenseManager");
		_ventilationAnalytiqueManager = ventilationAnalytiqueManager ?? throw new ArgumentNullException("ventilationAnalytiqueManager");
		_alimentationCaisseManager = alimentationCaisseManager ?? throw new ArgumentNullException("alimentationCaisseManager");
		_deviseManager = deviseManager ?? throw new ArgumentNullException("deviseManager");
		_modeReglementManager = modeReglementManager ?? throw new ArgumentNullException("modeReglementManager");
		_utilisateurManager = utilisateurManager ?? throw new ArgumentNullException("utilisateurManager");
		_annexeManager = annexeManager ?? throw new ArgumentNullException("annexeManager");
		_dossierCommunRepository = dossierCommunRepository ?? throw new ArgumentNullException("dossierCommunRepository");
		_subscriberRepository = subscriberRepository ?? throw new ArgumentNullException("subscriberRepository");
		_typeCautionManager = typeCautionManager ?? throw new ArgumentNullException("TypeCautionManager");
		_informationBanqueManager = informationBanqueManager ?? throw new ArgumentNullException("informationBanqueManager");
		_extraitBancaireManager = extraitBancaireManager ?? throw new ArgumentNullException("extraitBancaireManager");
		_banqueTiersManager = banqueTiersManager ?? throw new ArgumentNullException("banqueTiersManager");
		_previsionnelManager = previsionnelManager ?? throw new ArgumentNullException("previsionnelManager");
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException("passwordHasher");
		_codeActiviteManager = codeActiviteManager ?? throw new ArgumentNullException("codeActiviteManager");
		_designationDocumentManager = designationDocumentManager ?? throw new ArgumentNullException("designationDocumentManager");
		_regleRetenueSourceTvaManager = regleRetenueSourceTvaManager ?? throw new ArgumentNullException("regleRetenueSourceTvaManager");
		_operationRetenuSourceManager = operationRetenuSourceManager;
		_creditManager = creditManager;
	}

	public bool CheckLogin(string login, string password)
	{
		if (string.IsNullOrEmpty(login))
		{
			return false;
		}
		Utilisateur byLogin = UtilisateurManager.GetByLogin(login);
		if (byLogin == null)
		{
			return false;
		}
		byte[] second = _passwordHasher.Hash(password, byLogin.Salt);
		byte[] second2 = _passwordHasher.HashOldVersion(password, byLogin.Salt);
		if (byLogin != null)
		{
			if (!byLogin.Hash.SequenceEqual(second))
			{
				return byLogin.Hash.SequenceEqual(second2);
			}
			return true;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is GroupeService groupeService))
		{
			return false;
		}
		return groupeService.GetHashCode() == GetHashCode();
	}

	public DossierCommun GetDossierCommun()
	{
		return _dossierCommunRepository.Get();
	}

	public override int GetHashCode()
	{
		return 17 * 23 + SocieteManager.GetHashCode();
	}

	public bool HasConnectedSession()
	{
		return _subscriberRepository.HasConnectedSession();
	}

	public IEnumerable<Subscriber> GetAllSession()
	{
		return _subscriberRepository.GetAll();
	}

	public void ThrowIfConnectedSession()
	{
		if (!HasConnectedSession())
		{
			return;
		}
		IEnumerable<Subscriber> allWithoutApplication = _subscriberRepository.GetAllWithoutApplication();
		StringBuilder stringBuilder = new StringBuilder("La migration est interrompue.\nVeuillez fermer toutes les sessions de l'application :");
		foreach (Subscriber item in allWithoutApplication)
		{
			stringBuilder.AppendLine(string.Empty).Append("- Utilisateur : ").Append(item.UtilisateurLogin)
				.Append(" Poste : ")
				.AppendLine(item.UtilisateurPoste);
		}
		throw new ApplicationException(stringBuilder.ToString());
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoGroupeService);
	}

	public void UpdateDossierCommun(string folderSurcharge)
	{
		DossierCommun dossierCommun = _dossierCommunRepository.Get();
		dossierCommun.DossierSurcharge = folderSurcharge;
		_dossierCommunRepository.Update(dossierCommun);
	}
}
