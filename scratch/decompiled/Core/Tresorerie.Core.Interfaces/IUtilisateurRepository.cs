using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IUtilisateurRepository
{
	int? Create(Utilisateur utilisateur);

	void Delete(Utilisateur utilisateur);

	Utilisateur Get(int no);

	Utilisateur Get(string login);

	IEnumerable<Utilisateur> GetAll();

	void Update(Utilisateur utilisateur);
}
