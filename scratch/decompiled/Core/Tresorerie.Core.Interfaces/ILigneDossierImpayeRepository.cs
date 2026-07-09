using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDossierImpayeRepository
{
	Task<IEnumerable<LigneDossierImpaye>> GetAllAsync(int dossierNo);

	Task<IEnumerable<LigneDossierImpaye>> GetAllAsync(int dossierNo, TypeLigneDossierImp type);

	LigneDossierImpaye GetByReglementNo(int reglementNo, int societeNo);

	IEnumerable<LigneDossierImpaye> GetAllByEcheanceNo(int echeanceNo, int societeNo);

	Task<IEnumerable<LigneDossierImpaye>> GetAllByEcheanceNoAsync(int echeanceNo, int societeNo);

	IEnumerable<LigneDossierImpaye> GetAllByEcheanceAchatNo(int echeanceNo, int societeNo);

	Task CreateAsync(IEnumerable<LigneDossierImpaye> lignes);

	Task<int> CreateAsync(LigneDossierImpaye ligne);

	int Create(LigneDossierImpaye ligne);

	Task<LigneDossierImpaye> GetAsync(int ligneNo);

	LigneDossierImpaye Get(int ligneNo);

	void DeleteLigne(int ligneNo);

	void UpdateLigne(int ligneNo, decimal montantImputer, int ordre);

	IEnumerable<LigneDossierImpaye> GetAll(int dossierNo, TypeLigneDossierImp type);
}
