using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IPropertyExtensionProvider
{
	IList<DbPropertyExtension> GetAllExtendedProperties(DbObjectType objectType, string propertyName);

	DbPropertyExtension GetExtendedProperty(DbObjectType objectType, string objectName, string propertyName);

	void AddExtendedProperty(DbObjectType objectType, string objectName, string propertyName, string value);

	void UpdateExtendedProperty(DbObjectType objectType, string objectName, string propertyName, string value);
}
