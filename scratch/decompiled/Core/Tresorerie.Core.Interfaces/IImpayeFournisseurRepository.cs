using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IImpayeFournisseurRepository
{
	ImpayeFournisseur Get(int reglementNo);

	ImpayeFournisseur GetByEcheance(int echeanceNo);

	IEnumerable<ImpayeFournisseur> GetAll(int societeNo);

	IEnumerable<ImpayeFournisseur> GetAllCompta(int societeNo);

	void Update(ImpayeFournisseur impaye);

	Task<IEnumerable<ImpayeFournisseur>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, bool isComptabilise, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	IEnumerable<ImpayeFournisseur> GetAll(int fournisseurNo, int societeNo);

	IEnumerable<ImpayeFournisseur> GetAll(int fournisseurNo, int societeNo, params int[] souchesNo);
}
