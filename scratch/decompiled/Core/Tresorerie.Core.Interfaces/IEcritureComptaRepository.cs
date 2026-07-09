using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEcritureComptaRepository
{
	int Create(EcritureComptable ecriture);

	Task CreateAsync(IEnumerable<EcritureComptable> ecritures);

	void DeleteLigneAlimentation(int alimentationNo);

	void DeleteLigneOperationBancaire(int operationBancaireNo);

	void DeleteLigneBordereau(int mvtNo);

	void DeleteLigne(int ecartNo, MouvementDomaine domaine);

	void DeleteLigneEcritureFrs(int mvtNo);

	void DeleteLigneImpaye(int reglementNo);

	void DeleteLigneMouvementDepense(int mouvementDepense);

	void DeleteLigneReglementClient(int mvtNo);

	void DeleteLigneReglementFournisseur(int mvtNo);

	void DeleteLigneReglementTraiteFournisseur(int mvtNo, string codeJourna);

	void DeleteLigneRemboursementClient(int no);

	void DeleteLigneRemboursementFournisseur(int no);

	void DeleteLigneRemboursementDivers(int no);

	void DeleteLigneTransfert(int transfertNo);

	void DeleteLigneVirementInterne(int virementInterneNo);

	void DeleteLigneVirementTiers(int virementTiersNo);

	void DeleteRemplacement(int mvtNo);

	string EcritureGetPieceNumero(int societeNo, int etape, string reference, int exercice, ErpComptaPeriode periode);

	EcritureComptable Get(int no);

	IList<EcritureComptable> Get(int societeNo, int annee, ErpComptaPeriode periode, string codeJournal);

	IList<EcritureComptable> GetAll(int mvtNo, MouvementDomaine domaine);

	IList<EcritureComptable> Get(int annee, int societeNo, MouvementDomaine domaine);

	IList<EcritureComptable> Get(int mouvementNo, int etape, MouvementDomaine domaine, NatureActionEtape natureAction);

	EcritureComptable GetByErpNo(int erpNo, int societeNo);

	IList<EcritureComptable> GetByTiers(int societeNo, string fournisseurNumero, int annee);

	IList<EcritureComptable> GetEcheance(int societeNo);

	IList<EcritureComptable> GetEcritureEnAttente(int mouvementNo, MouvementDomaine domaine);

	IList<EcritureComptable> GetEcritureRemplacement(int reglementNo);

	IList<EcritureComptable> GetEcrituresEcart(int dossierNo, string dossierNumero, MouvementDomaine domaine);

	IList<EcritureComptable> GetEcrituresRemboursement(int mvtNo);

	IList<EcritureComptable> GetEcrituresRemboursementFournisseur(int mvtNo);

	IList<EcritureComptable> GetEcrituresVirMasse(int mvtNo);

	IList<EcritureComptable> GetNextEtape(int mouvementNo, int etape, MouvementDomaine domaine);

	IList<EcritureComptable> GetPreviousEtape(int mouvementNo, int etape, MouvementDomaine domaine);

	void LettreEcritures(int[] ecrituresNo, string lettre);

	void DeLettrerEcritures(int[] ecrituresNo);

	void UpdateEcriture(EcritureComptable ecriture);

	void DeleteLigneFactureFournisseurTresorerie(int no);

	IList<EcritureComptable> GetEcritureDeletedOpearationBancaire(int societeNo);

	IList<EcritureComptable> GetAllByDocumentNumero(string numDocument, MouvementDomaine domaine);

	void DeleteEcriture(int ecritureNo);
}
