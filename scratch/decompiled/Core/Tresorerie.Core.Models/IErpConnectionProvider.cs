using System.Data;

namespace Tresorerie.Core.Models;

public interface IErpConnectionProvider
{
	void Change(ErpConnection configuration);

	IDbConnection Get();

	ErpConnection GetCurrentConfiguration();

	bool HasConnectionString();
}
