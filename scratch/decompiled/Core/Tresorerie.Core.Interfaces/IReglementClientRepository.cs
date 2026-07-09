using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IReglementClientRepository
{
	void AnnulerReglement(int reglementNo, bool isAnnule);

	int? Create(ReglementClient reglement);

	void Delete(ReglementClient reglement);

	ReglementClient Get(int no);

	ReglementClient Get(int societeNo, string numero);

	IEnumerable<ReglementClient> GetAll(int societeNo, EtatComptabilite etat);

	IEnumerable<ReglementClient> GetAllToDeclarationTvaEncaissement(int societeNo, DateTime dateDebutExercice, DateTime dateFin);

	IEnumerable<ReglementClient> GetAll(int societeNo, EtatComptabilite etat, int exercice, int caisseNo, int modeNo, DateTime dateMin, DateTime dateMax, DateTime echeanceMin, DateTime echeanceMax, bool parRapportDateRemise, Remis[] remis, TiersType[] tiersType);

	IEnumerable<ReglementClient> GetAll(int societeNo, TiersType[] tiersType, Etat? etat, Remis? remis, DateTime? dateDebut, params int[] modesNo);

	IEnumerable<ReglementClient> GetAll(int societeNo, TiersType[] tiersType, DateTime? dateDebut, params int[] caissesNo);

	IEnumerable<ReglementClient> GetAll(int societeNo, DateTime dateDebut, DateTime dateFin, params int[] caissesNo);

	IEnumerable<ReglementClient> GetAllNonAnnule(int societeNo, DateTime dateDebut, DateTime dateFin, params int[] caissesNo);

	IEnumerable<ReglementClient> GetAllByDateEcheance(int societeNo, DateTime echeanceDebut, DateTime echeanceFin, params int[] caissesNo);

	IEnumerable<ReglementClient> GetAll(int societeNo, int clientNo, EtatComptabilite etat, DateTime dateMin, DateTime dateMax);

	IEnumerable<ReglementClient> GetAllByBanque(int societeNo, int banqueNo);

	IEnumerable<ReglementClient> GetAllByCaisse(int caisseNo, Etat? etat, Remis? remis, params int[] modesNo);

	IEnumerable<ReglementClient> GetAllByCaisse(int[] caisseNo, Etat? etat, Remis? remis, params int[] modesNo);

	IEnumerable<ReglementClient> GetAllByClient(int societeNo, int clientNo, Etat? etat, Remis? remis, params int[] modesNo);

	IEnumerable<ReglementClient> GetAllByClient(int societeNo, int clientNo, DateTime dateMin, DateTime dateMax, Etat? etat, Remis? remis, params int[] modesNo);

	IEnumerable<ReglementClient> GetAllByClient(int societeNo, int clientNo, Etat? etat, params int[] caissesNo);

	IEnumerable<ReglementClient> GetAllByInfo4Libelle(int societeNo, string sessionNo);

	IEnumerable<ReglementClient> GetAllReglementEscompte(int societeNo, int banqueNo, DateTime dateMin, DateTime dateMax, bool isRegler);

	IEnumerable<ReglementClient> GetReglements(int[] reglementsNo);

	IEnumerable<ReglementClient> GetAllPieceNonEchuByClient(int societeNo, int tiersNo, int[] caissesNo);

	decimal GetSoldeReglementClient(int societeNo, int clientNo);

	void Update(ReglementClient reglement);

	void UpdateErpSynchro(ReglementClient reglement);

	void UpdateAncienneDateEcheance(ReglementClient reglement);

	void ReporterReglementClient(ReglementClient reglement);

	Task<IEnumerable<ReglementClient>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, Remis[] remis, bool? pointe, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementClient>> GetAllAComptaByDateEcheanceAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, Remis[] remis, bool? pointe, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementClient>> GetAllAComptaByDateRemisAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, bool? pointe, int societeNo, CancellationToken cancellationToken);

	decimal GetMontantPreavisClient(int societeNo, int clientNo);

	IEnumerable<ReglementClient> GetAllSynchroErp(int societeNo, DateTime dateDebut, DateTime dateFin, bool isSynchroniser);

	bool IsReferenceExiste(int societeNo, string reference, int reglementNo);

	void UpdateReservation(int no, bool IsReserver);

	bool IsReglementValid(int reglementNo, int societeNo);

	void UpdateReglementValid(int reglementNo, bool isValid);

	IEnumerable<ReglementClient> GetAllReglementClientNonSolde(int no, int tiersNo, int caisseNo);

	IEnumerable<ReglementClient> GetAllReglementHasErpNo(int societeNo, DateTime? dateDebut = null, DateTime? dateFin = null);

	ReglementClient GetByErpNo(int erpReglementNo, int societeNo);
}
