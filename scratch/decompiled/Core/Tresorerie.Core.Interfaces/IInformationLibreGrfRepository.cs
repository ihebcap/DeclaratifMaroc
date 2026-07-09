using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IInformationLibreGrfRepository
{
	InformationLibre Get(int societeNo);

	void Update(InformationLibre informationLibre);
}
