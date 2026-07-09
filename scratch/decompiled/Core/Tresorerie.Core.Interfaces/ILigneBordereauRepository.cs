using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneBordereauRepository
{
	void Add(LigneBordereau ligne, int? banqueNo);

	void Delete(int ligneNo);

	LigneBordereau Get(int no);

	void Update(LigneBordereau ligne, int? banqueNo, bool isRemis);
}
