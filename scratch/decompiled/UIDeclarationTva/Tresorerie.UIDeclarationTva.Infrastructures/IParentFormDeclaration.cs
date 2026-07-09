using Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;
using Tresorerie.UIDeclarationTva.Stuctures;

namespace Tresorerie.UIDeclarationTva.Infrastructures;

public interface IParentFormDeclaration
{
	void OpenDeclarationTvaEncaissement(DeclarationTvaEncaissementView view);

	void OpenDeclarationDelaisPaiement(DeclarationDelaisPaiementView view);
}
