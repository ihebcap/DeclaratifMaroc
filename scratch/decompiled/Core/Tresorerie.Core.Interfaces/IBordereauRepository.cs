using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IBordereauRepository
{
	int Create(Bordereau bordereau);

	void Delete(Bordereau bordereau);

	bool Exists(int societeNo, string numero);

	Bordereau Get(int no);

	Bordereau Get(int caisseNo, string numero);

	IEnumerable<Bordereau> GetAll(int caisseNo);

	IEnumerable<int> GetAllBordereauNoComptabilisationEnMasse(int societeNo, int[] banqueNos, int[] caisseNo, int[] typeBordereauNo, DateTime dateDe, DateTime dateA, bool isComptabilise);

	IEnumerable<Bordereau> GetAllByBanque(int banqueNo, int societeNo);

	IEnumerable<Bordereau> GetAllBySociete(int societeNo);

	IEnumerable<Bordereau> GetAllBySociete(int societeNo, params int[] caissesNo);

	Bordereau GetByNumero(int societeNo, string numero);

	void Update(Bordereau bordereau);

	Task<IEnumerable<Bordereau>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] typeBordereauxNo, int[] banquesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<Bordereau>> GetAllAComptaByDateRemisAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] typeBordereauxNo, int[] banquesNo, int societeNo, CancellationToken cancellationToken);

	IEnumerable<Bordereau> GetAllToLettrer(int societeNo, DateTime minDate, DateTime maxDate, params int[] caissesNo);
}
