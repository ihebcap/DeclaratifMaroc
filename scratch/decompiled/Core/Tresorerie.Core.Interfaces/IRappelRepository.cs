using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRappelRepository
{
	int Create(Rappel rappel);

	void Delete(Rappel rappel);

	Rappel Get(int no);

	Rappel Get(int societeNo, string numero);

	IEnumerable<Rappel> GetAll(int societeNo);

	IEnumerable<Rappel> GetAllByClient(int societeNo, int clientNo);

	IEnumerable<Rappel> GetAllParents(int societeNo, int clientNo, DomaineRappel domaine);

	IEnumerable<Rappel> GetAllRappelEchence(int societeNo, int echeanceNo);

	IEnumerable<Rappel> GetAllRecouvrementClotureBeforeDate(int societeNo, DateTime date, bool isCloture);

	IEnumerable<Rappel> GetByParentId(int societeNo, int parentNo);

	void Update(Rappel rappel);
}
