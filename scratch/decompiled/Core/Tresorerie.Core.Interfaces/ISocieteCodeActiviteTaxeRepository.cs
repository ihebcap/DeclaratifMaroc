using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteCodeActiviteTaxeRepository
{
	List<SocieteCodeActiviteTaxe> GetAll(int societeNo);

	SocieteCodeActiviteTaxe Get(int no);

	SocieteCodeActiviteTaxe Get(int societeNo, string erpTaxe);

	void Create(SocieteCodeActiviteTaxe societeCodeActiviteTaxe);

	void Delete(SocieteCodeActiviteTaxe societeCodeActiviteTaxe);

	bool IsTaxeMapped(int societeNo, string erpTaxeCode);
}
