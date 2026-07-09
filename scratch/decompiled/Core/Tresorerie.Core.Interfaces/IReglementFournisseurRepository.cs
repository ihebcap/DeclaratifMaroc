using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IReglementFournisseurRepository
{
	ReglementFournisseur Get(int no);

	ReglementFournisseur Get(int societeNo, string numero);

	ReglementFournisseur GetRetenue(int societeNo, string numero);

	IEnumerable<ReglementFournisseur> GetAll(int societeNo);

	IEnumerable<ReglementFournisseur> GetAll(int societeNo, DateTime dateDebut, DateTime dateFin);

	IEnumerable<ReglementFournisseur> GetAllByDateEcheance(int societeNo, DateTime echeanceDebut, DateTime echeanceFin, params int[] caissesNo);

	IEnumerable<ReglementFournisseur> GetAll(int societeNo, EtatComptabilite etat);

	IEnumerable<ReglementFournisseur> GetAllToDeclarationTvaEncaissement(int societeNo, DateTime dateDebutExercice, DateTime dateFin);

	IEnumerable<ReglementFournisseur> GetAllPieceNonEchu(int societeNo, int[] caissesNo);

	IEnumerable<ReglementFournisseur> GetAllPieceNonEchuByFournisseur(int societeNo, int tiersNo, int[] caissesNo);

	IEnumerable<ReglementFournisseur> GetReglementFournisseurs(int societeNo, int tiersNo);

	IEnumerable<ReglementFournisseur> GetAllReglementFournisseursNonSolde(int societeNo, int tiersNo, int caisseNo);

	IEnumerable<ReglementFournisseur> GetAllReglementFournisseursNonSolde(int societeNo);

	bool IsUsedInDossierFrs(int reglementNo, int societeNo);

	bool VerifyCanDecompatabiliser(int reglemmentNo, int societeNo);

	bool IsReglementValid(int reglementNo, int societeNo);

	int? Create(ReglementFournisseur reglement);

	void Delete(ReglementFournisseur reglement);

	void Update(ReglementFournisseur reglement);

	void UpdateErpSynchro(ReglementFournisseur reglement);

	void UpdateEtatCompta(int no, EtatComptabilite etat, int modificateurNo);

	void UpdateEtatImpaye(ReglementFournisseur reglement);

	void PointerTraite(int mouvementNo, bool isPointe, DateTime datePointage);

	void UpdateRecuperation(int no, RemisFournisseur remis, DateTime dateRecuperation, string recouvreurCin, string recouvreurNom, int modificateurNo);

	void UpdateSignature(int no, bool pieceSigne, int modificateurNo);

	void UpdateEtatComptaTraiteFrs(int no, EtatComptabilite etat, int modificateurNo);

	void UpdateAnnexe(int no, int annexeType, string codeAnnexe);

	void UpdateReglementDate(int no, int modificateurNo, DateTime newDate);

	void UpdateDossierReglement(int no, int? dossierNo, string dossierNumero, int modoficateurNo);

	void UpdateReservation(int reglementNo, bool reserve);

	void UpdateReglementValid(int reglementNo, bool isValid);

	void AnnulerReglement(int reglementNo, bool isAnnule);

	void UpdateAncienneDateEcheance(ReglementFournisseur reglement);

	void ReporterReglement(ReglementFournisseur reglement);

	decimal GetSoldeReglementFournisseur(int societeNo, int fournisseurNo);

	Task<IEnumerable<ReglementFournisseur>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementFournisseur>> GetAllDecaisseByEcheanceAComptaAsync(DateTime dateEcheanceMin, DateTime dateEcheanceMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementFournisseur>> GetAllDecaisseByRapprochementAComptaAsync(DateTime dateRapprochementMin, DateTime dateRapprochementMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementFournisseur>> GetAllDecaisseByRecuperationAComptaAsync(DateTime dateRecuperationMin, DateTime dateRecuperationMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<ReglementFournisseur>> GetAllByDateRecuperationAComptaAsync(DateTime dateRecupMin, DateTime dateRecupMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	IEnumerable<ReglementFournisseur> GetAllNonSynchroErp(int societeNo, DateTime dateDebut, DateTime dateFin, bool isSynchroniser);

	IEnumerable<ReglementFournisseur> GetAll(int societeNo, int caisseNo, int banqueNo, int deviseNo);

	IEnumerable<ReglementFournisseur> GetAllReglementHasErpNo(int societeNo, DateTime? dateDebut = null, DateTime? dateFin = null);
}
