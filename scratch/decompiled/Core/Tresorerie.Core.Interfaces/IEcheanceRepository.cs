using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEcheanceRepository
{
	void Ajuster(int no, bool isAjuste = true);

	int? Create(Echeance echeance);

	void Delete(Echeance echeance);

	Echeance Get(int no);

	Echeance Get(int societeNo, string echeanceNumero, EcheanceType echeanceType);

	Task<IEnumerable<Echeance>> GetAllAsync(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType, int clientNo);

	Task<IEnumerable<Echeance>> GetAllAsync(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType, int clientNo, int[] souchesNo);

	Task<IEnumerable<Echeance>> GetAllAsync(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType);

	Task<IEnumerable<Echeance>> GetAllAsync(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType, int[] souchesNo);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, bool nonPaye, bool onlySpe = false, DateTime? dateDebut = null, DateTime? dateFin = null);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, bool nonPaye, DateTime echeanceDebut, bool onlySpe = false);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, bool nonPaye, bool onlySpe = false, DateTime? dateDebut = null, DateTime? dateFin = null, params int[] souchesNo);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType, DateTime? dateDebut = null, DateTime? dateFin = null, bool isImporter = false, params int[] souchesNo);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, bool nonPaye, DateTime echeanceDebut, bool onlySpe = false, params int[] souchesNo);

	IEnumerable<Echeance> GetAllByTiers(ErpDomaine domaine, int societeNo, int tiersNo, Etat? etat = null);

	IEnumerable<Echeance> GetAllByTiers(ErpDomaine domaine, int societeNo, int tiersNo, DateTime dateDu, DateTime dateAu, Etat? etat = null);

	IEnumerable<Echeance> GetAllByTiers(ErpDomaine domaine, int societeNo, int tiersNo, DateTime dateDu, DateTime dateAu, int designationDocumentNo, Etat? etat = null);

	int GetNombreImpayeByTiers(ErpDomaine domaine, int societeNo, string clientCode);

	int GetAllDepassementDetaisPayement(ErpDomaine domaine, int societeNo, string clientCode, int delaiPayement);

	IEnumerable<Echeance> GetAllByDocument(ErpDomaine domaine, int societeNo, string document, ErpDocumentType type, int tiersNo, int soucheNo);

	IEnumerable<Echeance> GetAllByDocument(ErpDomaine domaine, int societeNo, string document);

	Echeance GetByErpNo(ErpDomaine domaine, int societeNo, string document, int erpNo);

	int GetEcheanceEcart(int echeanceNo);

	int GetEcheanceRemboursement(int remboursementNo);

	int GetEcheanceRemboursementAvoirRS(int remboursementNo);

	bool HasEcheance(SocieteModeReglement mode);

	bool HasEcheance(SocieteDevise devise);

	void Update(Echeance echeance);

	void UpdateMontantFactureFournisseur(int factureNo, decimal montantDevise, decimal montantDeviseSociete, Etat etat);

	bool IsUsedInDossierFrs(int echeanceNo, int societeNo);

	void UpdateReservation(int echeanceNo, bool reserve);

	IEnumerable<Echeance> GetAllNonReserver(ErpDomaine domaine, int societeNo, bool nonPaye, bool onlySpe = false, DateTime? dateDebut = null, DateTime? dateFin = null);

	IEnumerable<Echeance> GetAllNonReserver(ErpDomaine domaine, int societeNo, bool nonPaye, bool onlySpe = false, DateTime? dateDebut = null, DateTime? dateFin = null, params int[] souchesNo);

	IEnumerable<Echeance> GetAllHasAffaire(ErpDomaine domaine, int societeNo, DateTime? dateDebut = null, DateTime? dateFin = null, bool hasNotErp = false);

	void UpdatePieceJointe(int echeanceNo, string fileName, byte[] filePdf);

	bool HasWorkflowValidation(int societeNo);

	IEnumerable<Echeance> GetAll(ErpDomaine domaine, int societeNo, EcheanceType[] echeancesType, DateTime? dateDebut = null, DateTime? dateFin = null, bool isImporter = false);
}
