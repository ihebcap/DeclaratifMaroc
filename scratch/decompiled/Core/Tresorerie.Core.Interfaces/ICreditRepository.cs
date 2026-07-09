using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICreditRepository
{
	Credit Get(int no);

	Credit Get(int societeNo, string numero);

	IEnumerable<Credit> GetAll(int societeNo);

	IEnumerable<Credit> GetAll(int banqueNo, int societeNo, DateTime dateDebut, DateTime dateFin, NatureTypeCredit nature);

	IEnumerable<Credit> GetAllWithAffaire(int societeNo);

	int Create(Credit credit);

	void Delete(int no);

	void Update(Credit credit);

	void SetComptabiliser(int no, bool isComptabiliser);

	bool IsCreditUtiliseType(int typeNo);

	void AnnulerAccord(Credit credit);
}
