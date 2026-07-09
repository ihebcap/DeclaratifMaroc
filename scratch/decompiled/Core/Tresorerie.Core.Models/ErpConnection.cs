using System.Data.SqlClient;

namespace Tresorerie.Core.Models;

public class ErpConnection
{
	public string DatabaseName { get; set; }

	public string Password { get; set; }

	public string ServerName { get; set; }

	public TypeAuthentification Type { get; set; }

	public string User { get; set; }

	public string ErpUser { get; set; }

	public string ErpPassword { get; set; }

	public ErpConnection()
	{
	}

	public ErpConnection(SqlConnectionStringBuilder cnxBuilder)
	{
		ServerName = cnxBuilder.DataSource;
		DatabaseName = cnxBuilder.InitialCatalog;
		User = cnxBuilder.UserID;
		Password = cnxBuilder.Password;
		Type = ((!cnxBuilder.IntegratedSecurity) ? TypeAuthentification.Sql : TypeAuthentification.Windows);
		ErpUser = string.Empty;
		ErpPassword = string.Empty;
	}

	public string GetConnection()
	{
		string text = ((Type == TypeAuthentification.Sql) ? ("User id=" + User + ";Password=" + Password + ";") : "Integrated Security=SSPI;");
		return new SqlConnectionStringBuilder("Data Source=" + ServerName + ";Initial Catalog=" + DatabaseName + ";" + text + ";")
		{
			MultipleActiveResultSets = true,
			MinPoolSize = 3,
			ApplicationName = "AP Business Soft - AP Gestion des Règlements"
		}.ConnectionString;
	}

	public bool IsValid()
	{
		if (!string.IsNullOrEmpty(DatabaseName) && !string.IsNullOrEmpty(ServerName))
		{
			if (Type != TypeAuthentification.Sql || string.IsNullOrEmpty(User))
			{
				return Type == TypeAuthentification.Windows;
			}
			return true;
		}
		return false;
	}
}
