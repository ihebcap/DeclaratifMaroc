using System;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IUtilisateurGridRepository
{
	void Create(UtilisateurGrid utilisateurGrid);

	void Delete(UtilisateurGrid utilisateurGrid);

	UtilisateurGrid Get(int no);

	UtilisateurGrid Get(Guid guid, int utilisateurNo);

	void Update(UtilisateurGrid utilisateurGrid);
}
