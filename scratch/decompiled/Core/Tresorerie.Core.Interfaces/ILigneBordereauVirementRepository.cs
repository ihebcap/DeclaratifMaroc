using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneBordereauVirementRepository
{
	void Add(LigneBordereauVirement ligne, int? banqueNo);

	void Delete(int ligneNo);

	LigneBordereauVirement Get(int no);
}
