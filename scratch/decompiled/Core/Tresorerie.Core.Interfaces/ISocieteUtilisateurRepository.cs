using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteUtilisateurRepository
{
	void Create(int societeNo, int utilisateurNo);

	void Delete(int societeNo, int utilisateurNo);

	IEnumerable<Utilisateur> GetAll(int societeNo);

	bool HasMouvement(int userNo, int societeNo);
}
