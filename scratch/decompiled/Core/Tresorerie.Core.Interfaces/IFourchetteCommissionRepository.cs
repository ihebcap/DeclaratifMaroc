using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IFourchetteCommissionRepository
{
	List<FourchetteCommission> GetAll(int societeNo);

	FourchetteCommission Get(int no);

	int Create(FourchetteCommission fourchetteCommission);

	void Update(FourchetteCommission fourchetteCommission);

	void Delete(FourchetteCommission fourchetteCommission);
}
