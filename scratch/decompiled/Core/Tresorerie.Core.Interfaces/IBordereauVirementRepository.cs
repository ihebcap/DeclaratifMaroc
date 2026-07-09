using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IBordereauVirementRepository
{
	int Create(BordereauVirement bordereau);

	void Delete(BordereauVirement bordereau);

	bool Exists(int societeNo, string numero);

	BordereauVirement Get(int no);

	BordereauVirement Get(int caisseNo, string numero);

	IEnumerable<BordereauVirement> GetAll(int caisseNo);

	IEnumerable<int> GetAllBordereauNoComptabilisationEnMasse(int societeNo, int[] banqueNos, int[] caisseNo, DateTime dateDe, DateTime dateA, bool isComptabilise);

	IEnumerable<BordereauVirement> GetAllByBanque(int banqueNo, int societeNo);

	IEnumerable<BordereauVirement> GetAllBySociete(int societeNo);

	IEnumerable<BordereauVirement> GetAllBySociete(int societeNo, params int[] caissesNo);

	BordereauVirement GetByNumero(int societeNo, string numero);

	void Update(BordereauVirement bordereau);

	Task<IEnumerable<BordereauVirement>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] banquesNo, int societeNo, CancellationToken cancellationToken);

	Task<IEnumerable<BordereauVirement>> GetAllAComptaByDateRemisAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] banquesNo, int societeNo, CancellationToken cancellationToken);
}
