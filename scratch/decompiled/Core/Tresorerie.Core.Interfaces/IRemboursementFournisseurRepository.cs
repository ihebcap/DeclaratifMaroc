using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRemboursementFournisseurRepository
{
	RemboursementFournisseur Get(int no);

	RemboursementFournisseur GetByReglementNo(int reglementNo);

	IEnumerable<RemboursementFournisseur> GetAll(int no);

	void UpdateEtatComptabilisation(int mvtNo, EtatComptabilite etatComptabilite);

	Task<IEnumerable<RemboursementFournisseur>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int societeNo, CancellationToken cancellationToken);
}
