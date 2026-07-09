using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;

namespace Tresorerie.Core.Services;

public class InformationBanqueManager
{
	private readonly IInformationsBanqueRepository _informationsBanqueRepository;

	private readonly ICaisseBanqueRepository _caisseBanqueRepository;

	private readonly IStructureVirementRepository _structureVirementRepository;

	public GroupeService Groupe { get; set; }

	public InformationBanqueManager(IInformationsBanqueRepository informationsBanqueRepository, ICaisseBanqueRepository caisseBanqueRepository, IStructureVirementRepository structureVirementRepository)
	{
		_informationsBanqueRepository = informationsBanqueRepository ?? throw new ArgumentNullException("informationsBanqueRepository");
		_caisseBanqueRepository = caisseBanqueRepository ?? throw new ArgumentNullException("caisseBanqueRepository");
		_structureVirementRepository = structureVirementRepository ?? throw new ArgumentNullException("structureVirementRepository");
	}

	public int Create(int banqueNo, decimal encoursEscompteAutorise, int nbrJoursCouverture, DateTime dateIntialSolde, decimal soldeInitial, bool defaultBanque, bool isEcritureCumule, IEnumerable<InformationsBanqueBordereau> bordereaux, bool isEnSommet, bool isComptaFraisCumule, string codeRegroupement, string adresse, decimal faciliteCaisse, string banqueIce, string prefixEntete, string prefixDetail, string prefixPied, TypeFichierBordoreau extensionFichier, int TailleEntete, int TailleDetail, int TaillePied, bool utiliseOrdreVirement, string orderVirementCompteGeneral, string orderVirementJournal, decimal quota, DateTime dateQuota, string banqueIdentifiant, Societe societe = null)
	{
		societe = societe ?? Groupe.SocieteManager.Societe;
		if (banqueNo <= 0)
		{
			throw new ArgumentException("la banque No est invalide!");
		}
		if (encoursEscompteAutorise < 0m)
		{
			throw new ArgumentException("le montant de l'encours escompté autorisé est invalide!");
		}
		if (nbrJoursCouverture < 0)
		{
			throw new ArgumentException("le nombre de jour de couverture est invalide!");
		}
		if (faciliteCaisse < 0m)
		{
			throw new ApplicationException("Facilité de caisse invalide.");
		}
		if (utiliseOrdreVirement && string.IsNullOrEmpty(orderVirementCompteGeneral))
		{
			throw new ApplicationException("Compte comptable invalide.");
		}
		if (utiliseOrdreVirement && string.IsNullOrEmpty(orderVirementJournal))
		{
			throw new ApplicationException("Journal invalide.");
		}
		if (quota < 0m)
		{
			throw new ApplicationException("Le montant de quota est invalide.");
		}
		dateQuota.Verifier("quota");
		InformationsBanque informationsBanque = new InformationsBanque
		{
			SocieteNo = societe.No,
			BanqueNo = banqueNo,
			EncoursEscompteAutorise = encoursEscompteAutorise,
			NbrJoursCouverture = nbrJoursCouverture,
			DateSolde = dateIntialSolde,
			SoldeInitial = soldeInitial,
			DefaultBanque = defaultBanque,
			IsEcritureCumule = isEcritureCumule,
			EnSommeil = isEnSommet,
			IsComptaFraisCumule = isComptaFraisCumule,
			CodeRegroupement = codeRegroupement,
			AdresseCompte = adresse,
			FaciliteCaisse = faciliteCaisse,
			BanqueIce = banqueIce,
			BanqueIdentifiant = banqueIdentifiant,
			PrefixDetail = prefixEntete,
			PrefixEntete = prefixDetail,
			PrefixPied = prefixPied,
			ExtensionFichier = extensionFichier,
			TailleEntete = TailleEntete,
			TailleDetail = TailleDetail,
			TaillePied = TaillePied,
			OrderVirementCompteGeneral = orderVirementCompteGeneral,
			OrderVirementJournal = orderVirementJournal,
			IsUtiliseOrdreVirement = utiliseOrdreVirement,
			Quota = quota,
			DateQuota = dateQuota
		};
		int num = _informationsBanqueRepository.Create(informationsBanque);
		if (num <= 0)
		{
			throw new ApplicationException("Ajout de l'information banque invalide!");
		}
		foreach (InformationsBanqueBordereau item in bordereaux)
		{
			item.InformationsBanqueNo = num;
			_informationsBanqueRepository.Create(item);
		}
		return num;
	}

	public void Update(int no, decimal encoursEscompteAutorise, int nbrJoursCouverture, DateTime dateSoldeIntial, decimal soldeInitial, bool defaultBanque, bool isEcritureCumule, IEnumerable<InformationsBanqueBordereau> bordereaux, bool isEnSommet, bool isComptaFraisCumule, string codeRegroupement, string adresse, decimal faciliteCaisse, string banqueIce, string prefixEntete, string prefixDetail, string prefixPied, TypeFichierBordoreau extensionFichier, int tailleEntete, int tailleDetail, int taillePied, bool utiliseOrdreVirement, string orderVirementcompteGeneral, string orderVirementJournal, decimal quota, DateTime dateQuota, string banqueIdentifiant)
	{
		if (no <= 0)
		{
			throw new ArgumentException("le numero de information banque est invalide!");
		}
		InformationsBanque informationsBanque = _informationsBanqueRepository.Get(no);
		if (informationsBanque == null)
		{
			throw new ArgumentException("Les informations de la banque est invalide!");
		}
		if (encoursEscompteAutorise < 0m)
		{
			throw new ArgumentException("le montant de l'encours escompté autorisé est invalide!");
		}
		if (nbrJoursCouverture < 0)
		{
			throw new ArgumentException("le nombre de jour de couverture est invalide!");
		}
		if (faciliteCaisse < 0m)
		{
			throw new ApplicationException("Facilité de caisse invalide.");
		}
		if (utiliseOrdreVirement && string.IsNullOrEmpty(orderVirementcompteGeneral))
		{
			throw new ApplicationException("Compte comptable invalide.");
		}
		if (utiliseOrdreVirement && string.IsNullOrEmpty(orderVirementJournal))
		{
			throw new ApplicationException("Journal invalide.");
		}
		if (quota < 0m)
		{
			throw new ApplicationException("Le montant de quota est invalide.");
		}
		dateQuota.Verifier("quota");
		informationsBanque.EncoursEscompteAutorise = encoursEscompteAutorise;
		informationsBanque.NbrJoursCouverture = nbrJoursCouverture;
		informationsBanque.DateSolde = dateSoldeIntial;
		informationsBanque.SoldeInitial = soldeInitial;
		informationsBanque.DefaultBanque = defaultBanque;
		informationsBanque.IsEcritureCumule = isEcritureCumule;
		informationsBanque.EnSommeil = isEnSommet;
		informationsBanque.IsComptaFraisCumule = isComptaFraisCumule;
		informationsBanque.CodeRegroupement = codeRegroupement;
		informationsBanque.AdresseCompte = adresse;
		informationsBanque.FaciliteCaisse = faciliteCaisse;
		informationsBanque.BanqueIce = banqueIce;
		informationsBanque.PrefixEntete = prefixEntete;
		informationsBanque.PrefixDetail = prefixDetail;
		informationsBanque.PrefixPied = prefixPied;
		informationsBanque.ExtensionFichier = extensionFichier;
		informationsBanque.TailleEntete = tailleEntete;
		informationsBanque.TailleDetail = tailleDetail;
		informationsBanque.TaillePied = taillePied;
		informationsBanque.OrderVirementCompteGeneral = orderVirementcompteGeneral;
		informationsBanque.OrderVirementJournal = orderVirementJournal;
		informationsBanque.IsUtiliseOrdreVirement = utiliseOrdreVirement;
		informationsBanque.Quota = quota;
		informationsBanque.DateQuota = dateQuota;
		informationsBanque.BanqueIdentifiant = banqueIdentifiant;
		_informationsBanqueRepository.Update(informationsBanque);
		_informationsBanqueRepository.DeleteLigne(no);
		foreach (InformationsBanqueBordereau item in bordereaux)
		{
			_informationsBanqueRepository.Create(item);
		}
	}

	public void Delete(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("le numero de information banque est invalide!");
		}
		InformationsBanque informationsBanque = _informationsBanqueRepository.Get(no);
		if (informationsBanque == null)
		{
			throw new ArgumentException("Les informations de la banque est invalide!");
		}
		_informationsBanqueRepository.Delete(informationsBanque);
	}

	public IList<InformationsBanque> GetAllBySociete(Societe societe)
	{
		SocieteManager societeManager = Groupe.SocieteManager;
		societe = societe ?? societeManager.Societe;
		List<InformationsBanque> list = _informationsBanqueRepository.GetAll(societe.No).ToList();
		foreach (InformationsBanque item in list)
		{
			item.Bordereaux = new BindingList<InformationsBanqueBordereau>(_informationsBanqueRepository.GetAllLignes(item.No).ToList());
			item.StructureBordereauxVirementTxt = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.txt, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			item.StructureBordereauxVirementXml = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.xml, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			item.StructureBordereauxVirementExcel = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.xlsx, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
		}
		return list;
	}

	public IList<InformationsBanque> GetAll()
	{
		List<InformationsBanque> list = _informationsBanqueRepository.GetAll().ToList();
		Societe societe = Groupe.SocieteManager.Societe;
		foreach (InformationsBanque item in list)
		{
			item.Bordereaux = new BindingList<InformationsBanqueBordereau>(_informationsBanqueRepository.GetAllLignes(item.No).ToList());
			item.StructureBordereauxVirementTxt = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.txt, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			item.StructureBordereauxVirementXml = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.xml, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			item.StructureBordereauxVirementExcel = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(item.BanqueNo, TypeFichierBordoreau.xlsx, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
		}
		return list;
	}

	public InformationsBanque Get(int no)
	{
		InformationsBanque informationsBanque = _informationsBanqueRepository.Get(no);
		Societe societe = Groupe.SocieteManager.Societe;
		if (informationsBanque != null)
		{
			informationsBanque.Bordereaux = new BindingList<InformationsBanqueBordereau>(_informationsBanqueRepository.GetAllLignes(informationsBanque.No).ToList());
			informationsBanque.StructureBordereauxVirementTxt = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(informationsBanque.BanqueNo, TypeFichierBordoreau.txt, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			informationsBanque.StructureBordereauxVirementXml = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(informationsBanque.BanqueNo, TypeFichierBordoreau.xml, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			informationsBanque.StructureBordereauxVirementExcel = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(informationsBanque.BanqueNo, TypeFichierBordoreau.xlsx, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
		}
		return informationsBanque;
	}

	public InformationsBanque GetByBanqueId(int banqueId)
	{
		Societe societe = Groupe.SocieteManager.Societe;
		InformationsBanque byErpNo = _informationsBanqueRepository.GetByErpNo(societe.No, banqueId);
		if (byErpNo != null)
		{
			byErpNo.Bordereaux = new BindingList<InformationsBanqueBordereau>(_informationsBanqueRepository.GetAllLignes(byErpNo.No).ToList());
			byErpNo.StructureBordereauxVirementTxt = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(byErpNo.BanqueNo, TypeFichierBordoreau.txt, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
			byErpNo.StructureBordereauxVirementXml = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(byErpNo.BanqueNo, TypeFichierBordoreau.xml, societe.No)
				orderby x.Position
				select x).ToList());
			byErpNo.StructureBordereauxVirementExcel = new BindingList<StructureVirement>((from x in _structureVirementRepository.GetAll(byErpNo.BanqueNo, TypeFichierBordoreau.xlsx, societe.No)
				orderby x.Emplacement, x.Position
				select x).ToList());
		}
		return byErpNo;
	}

	public void AddTypeBordereau(InformationsBanqueBordereau banqueBordereau)
	{
		if (banqueBordereau == null)
		{
			throw new ArgumentNullException("banqueBordereau");
		}
		InformationsBanque obj = Get(banqueBordereau.InformationsBanqueNo) ?? throw new ApplicationException($"Impossible de charger l'information banque [{banqueBordereau.InformationsBanqueNo}]");
		if (Groupe.BordereauxTypeManager.Get(banqueBordereau.TypeBordereauNo) == null)
		{
			throw new ApplicationException($"Impossible de charger le  type bordereau [{banqueBordereau.TypeBordereauNo}]");
		}
		if (obj.Bordereaux.Any((InformationsBanqueBordereau x) => x.TypeBordereauNo == banqueBordereau.TypeBordereauNo && x.InformationsBanqueNo == banqueBordereau.InformationsBanqueNo))
		{
			throw new ApplicationException("Type bordereau est déjà affecté !");
		}
		_informationsBanqueRepository.Create(banqueBordereau);
	}

	public InformationsBanque GetDefaultBanquePrevisionnel()
	{
		Societe societe = Groupe.SocieteManager.Societe;
		return _informationsBanqueRepository.GetDefaultBanquePrevisionnel(societe.No);
	}

	public InformationsBanque GetDefaultCaisseBanque(Caisse caisse)
	{
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		CaisseBanque caisseBanque = _caisseBanqueRepository.GetDefault(caisse);
		if (caisseBanque == null)
		{
			return null;
		}
		return Get(caisseBanque.InformationBanqueNo);
	}

	public StructureVirement GetStructureVirement(int no)
	{
		return _structureVirementRepository.Get(no);
	}

	public IEnumerable<StructureVirement> GetAllStructureVirementByBanque(int banqueNo, TypeFichierBordoreau typeFichier, Societe societe = null)
	{
		Societe societe2 = societe;
		if (societe2 == null)
		{
			societe2 = Groupe.SocieteManager.Societe;
		}
		return _structureVirementRepository.GetAll(banqueNo, typeFichier, societe2.No);
	}

	public int CreateStructureVirement(StructureVirement structure)
	{
		if (structure == null)
		{
			throw new ArgumentNullException("structure");
		}
		Societe societe = Groupe.SocieteManager.Societe;
		structure.SocieteNo = societe.No;
		if (structure.Position == 0)
		{
			throw new ApplicationException("Position invalide.");
		}
		if (structure.Taille == 0 && structure.TypeFichier == TypeFichierBordoreau.txt)
		{
			throw new ArgumentNullException("Taille invalide.");
		}
		if (string.IsNullOrEmpty(structure.BaliseDebut) && structure.TypeFichier == TypeFichierBordoreau.xml)
		{
			throw new ArgumentNullException("La balise de début est invalide.");
		}
		if (string.IsNullOrEmpty(structure.BaliseDebut) && structure.TypeFichier == TypeFichierBordoreau.xlsx)
		{
			throw new ArgumentNullException("le nom est invalide.");
		}
		return _structureVirementRepository.Create(structure);
	}

	public void UpdateStructureVirement(StructureVirement structure)
	{
		if (structure.Position <= 0)
		{
			throw new ApplicationException("Position invalide.");
		}
		if (structure.Taille == 0 && structure.TypeFichier == TypeFichierBordoreau.txt)
		{
			throw new ArgumentNullException("Taille invalide.");
		}
		_structureVirementRepository.Update(structure);
	}

	public void DeleteStructureVirement(int no)
	{
		_structureVirementRepository.Delete(no);
	}

	public void DeleteAllStructureVirementByBanque(int banqueNo, TypeFichierBordoreau typeFichier)
	{
		_structureVirementRepository.DeleteAllByBanque(banqueNo, typeFichier);
	}

	public void UpdateParamFichier(int banqueNo, string prefixEntet, string prefixDetail, string prefixPied, int tailleEntet, int tailleDetail, int taillePied, TypeFichierBordoreau type)
	{
		InformationsBanque informationsBanque = Get(banqueNo) ?? throw new ApplicationException("Impossible de charger la banque ");
		informationsBanque.PrefixEntete = prefixEntet;
		informationsBanque.PrefixDetail = prefixDetail;
		informationsBanque.PrefixPied = prefixPied;
		informationsBanque.TailleEntete = tailleEntet;
		informationsBanque.TailleDetail = tailleDetail;
		informationsBanque.TaillePied = taillePied;
		informationsBanque.ExtensionFichier = type;
		_informationsBanqueRepository.Update(informationsBanque);
	}
}
