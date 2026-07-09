using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDossierReglementRepository
{
	IEnumerable<DossierReglement> GetAll(DomaineDossier domaine, int societeNo, bool isDossierCommercial);

	IEnumerable<DossierReglement> GetAll(DomaineDossier domaine, int societeNo, int fournisseurNo, bool isDossierCommercial);

	IEnumerable<DossierReglement> GetAll(DomaineDossier domaine, TypeLigneDossier typeLigne, int entityNo);

	DossierReglement GetDossier(int dossierNo);

	DossierReglement GetDossier(DomaineDossier domaine, string numero, int societeNo);

	int Create(DossierReglement dossier);

	void Update(int dossierNo, string beneficiaire, string identifiant, NatureFournisseur natureBeneficiaire, string libelle, decimal montant, decimal cours, int? attestationRetenueNo = null);

	void Delete(int dossierNo);

	void Comptabiliser(int dossierNo);

	void Decomptabiliser(int dossierNo);

	void Lettre(int dossierNo, string lettre);

	void DeLettrer(int dossierNo);

	void UpdateDate(int dossierNo, DateTime date);

	void UpdateStatut(int dossierNo, StatutDossierReglement statut);

	void UpdatePhase(int dossierNo, PhaseDossierReglement etape);

	decimal GetMontantDossier(int dossierNo);

	void UpdateMontant(int dossierNo, decimal montant);

	void UpdateMontantEcart(int dossierNo, decimal montantEcart);

	IEnumerable<DossierReglement> GetAllAssocieeReglement(DomaineDossier domaine, int reglementNo);

	Task<IEnumerable<DossierReglement>> GetAllAComptaAsync(DomaineDossier domaine, DateTime dateMin, DateTime dateMax, bool isComptabilise, bool isDossierCommercial, StatutDossierReglement statut, int[] caissesNo, int societeNo, CancellationToken cancellationToken);

	bool HasDossierNonCloture(DomaineDossier domaine, int societeNo, DateTime dateDebut, DateTime dateFin);

	IEnumerable<DossierReglement> GetAllDossierFournisseurToLettrer(DateTime dateMin, DateTime dateMax, int societeNo);
}
