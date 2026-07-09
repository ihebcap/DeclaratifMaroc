using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICarnetTraiteRepository
{
	int Create(CarnetTraite carnetTraite);

	void Delete(CarnetTraite carnetTraite);

	CarnetTraite Get(int no);

	IEnumerable<CarnetTraite> GetAll(int societeNo);

	IEnumerable<CarnetTraite> GetAllBanqueActif(int societeNo);

	IEnumerable<CarnetTraite> GetAllByBanque(int banqueNo, int societeNo);

	IEnumerable<CarnetTraite> GetAllByBanqueActif(int banqueNo, int societeNo);

	void Update(CarnetTraite carnetTraite);
}
