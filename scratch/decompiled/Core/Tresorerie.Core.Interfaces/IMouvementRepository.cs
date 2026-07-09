using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvementRepository
{
	Mouvement Get(int no);

	Mouvement Get(string numero);

	void ModifyMouvement(int no, int utilisateurNo);
}
