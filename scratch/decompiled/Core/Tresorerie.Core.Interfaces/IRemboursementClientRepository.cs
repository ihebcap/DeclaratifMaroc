using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRemboursementClientRepository
{
	int? Create(RemboursementClient rembourcementClient);

	void Delete(RemboursementClient rembourcementClient);

	RemboursementClient Get(int no);

	IEnumerable<RemboursementClient> GetAllByLibelle(int societeNo, string session);

	void UpdateEtatComptabilisation(int no, EtatComptabilite etatComptabilite);
}
