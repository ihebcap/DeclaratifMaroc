using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAffectationRepository
{
	int? Create(Affectation affectation);

	void Delete(Affectation affectation);

	Affectation Get(int no);

	IEnumerable<Affectation> GetByEcartEcheance(int ecartNo);

	IEnumerable<Affectation> GetByEcheance(int echeanceNo);

	IEnumerable<Affectation> GetByReglement(int reglementNo);

	void Update(Affectation affectation, decimal montant, decimal montantDevise);

	void Update(Affectation affectation, int nbJourReglement, int delaisMoyenPayement);

	void SetDeclarationTva(Affectation affectation);

	void SetSynchronized(Affectation affectation, int erpNo, bool isSynchroniser);
}
