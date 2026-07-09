using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IInformationLibreRepository
{
	InformationLibre GetReglement(int societeNo);

	void UpdateReglement(InformationLibre informationLibre);

	void UpdateBordereau(InformationLibre informationLibre);

	InformationLibre GetBordereau(int societeNo);
}
