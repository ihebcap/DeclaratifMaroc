using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Dapper.Repositories;

public interface IOperationRetenuSourceRepository
{
	int? Create(OperationRetenuSource operationRetenuSource);

	void Delete(OperationRetenuSource operationRetenuSource);

	OperationRetenuSource Get(int no);

	OperationRetenuSource Get(string code);

	List<OperationRetenuSource> GetAll();

	bool IsUsed(int no);

	void Update(OperationRetenuSource operationRetenuSource);
}
