using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDossierReglementCommRepository
{
	LigneDossierReglementComm Get(int no);

	LigneDossierReglementComm Get(TypeLigneDossier type, int dossierNo, int entityNo);

	int GetLastOrdre(int dossierNo, TypeLigneDossier type);

	bool CanDecompatabiliser(int entityNo, TypeLigneDossier typeLigneDossier, int societeNo);

	IEnumerable<LigneDossierReglementComm> GetLigneEcheance(int dossierNo);

	IEnumerable<LigneDossierReglementComm> GetLigneReglement(int dossierNo);

	IEnumerable<LigneDossierReglementComm> GetAll(int dossierNo);

	IEnumerable<LigneDossierReglementComm> GetAllLigneDossiers(int societeNo);

	int Create(LigneDossierReglementComm ligne);

	void Delete(LigneDossierReglementComm ligne);

	void UpdateMontantAPayer(int ligneNo, decimal montantAPaye);

	void UpdateMontantSolde(int ligneNo, decimal solde);

	void UpdateOrdre(int entityNo, int ordre);
}
