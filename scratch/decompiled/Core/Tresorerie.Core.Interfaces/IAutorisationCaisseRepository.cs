using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAutorisationCaisseRepository
{
	void Create(int utilisateurNo, int caisseNo, ProfilType type);

	void Delete(int autorisationNo);

	AutorisationCaisse Get(int utilisateurNo, int caisseNo, ProfilType type);

	IEnumerable<AutorisationCaisse> GetAll(int utilisateurNo, int societeNo, ProfilType type);

	IEnumerable<AutorisationCaisse> GetAll(int utilisateurNo, int societeNo);

	void Update(int utilisateurNo, int caisseNo, bool isDefault, ProfilType type);
}
