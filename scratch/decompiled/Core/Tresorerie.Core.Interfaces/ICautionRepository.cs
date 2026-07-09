using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICautionRepository
{
	int Create(Caution caution);

	void Delete(Caution caution);

	Caution Get(int cautionNo);

	Caution GetByReglementNo(int reglementNo);

	IEnumerable<Caution> GetAll(int societeNo, DomaineCaution domaine);

	IEnumerable<Caution> GetAllByTier(int tierNo, DomaineCaution domaine);

	IEnumerable<Caution> GetAllByTierNonTransformer(int tierNo, DomaineCaution domaine);

	void Update(Caution caution);
}
