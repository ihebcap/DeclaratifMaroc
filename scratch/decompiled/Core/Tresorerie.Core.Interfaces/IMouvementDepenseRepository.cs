using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvementDepenseRepository
{
	int Create(MouvementDepense depense);

	void Delete(int no);

	MouvementDepense Get(int no);

	MouvementDepense Get(int societeNo, string numero);

	IEnumerable<MouvementDepense> GetAll(int societeNo);

	IEnumerable<MouvementDepense> GetAll(int societeNo, EtatComptabilite etat);

	IEnumerable<MouvementDepense> GetAllToDeclarationTvaEncaissement(int societeNo, DateTime dateDebutExercice, DateTime dateFin);

	Task<IEnumerable<MouvementDepense>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);

	void Update(MouvementDepense depense);
}
