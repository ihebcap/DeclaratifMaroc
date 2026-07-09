using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDossierCommunRepository
{
	DossierCommun Get();

	void Update(DossierCommun dossierCommun);
}
