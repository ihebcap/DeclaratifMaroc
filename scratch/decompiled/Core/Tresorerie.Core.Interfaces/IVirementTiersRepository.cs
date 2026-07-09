using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVirementTiersRepository
{
	void Comptabiliser(int virementNo);

	int Create(VirementTiers virement);

	void Decomptabiliser(int virementNo);

	void Delete(int virementNo);

	IEnumerable<VirementTiers> GetAll(int societeNo);

	IEnumerable<VirementTiers> GetAll(int societeNo, DateTime dateMin, DateTime DateMax);

	IEnumerable<VirementTiers> GetAllByLibelle(int societeNo, string session);

	VirementTiers GetVirement(int virementNo);

	VirementTiers GetVirement(string numero, int societeNo);

	void Update(int no, string libelle, string piece, decimal montant, decimal montantDeviseSociete);

	Task<IEnumerable<VirementTiers>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int societeNo, CancellationToken cancellationToken);
}
