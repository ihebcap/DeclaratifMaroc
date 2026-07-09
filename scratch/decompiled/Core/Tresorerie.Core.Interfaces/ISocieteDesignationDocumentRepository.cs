using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteDesignationDocumentRepository
{
	List<SocieteDesignationDocument> GetAll(int societeNo);

	SocieteDesignationDocument Get(int no);

	void Create(SocieteDesignationDocument societeDesignationDocument);

	void Delete(SocieteDesignationDocument societeDesignationDocument);

	bool IsErpIntituleMapped(int societeNo, string erpIntitule);

	bool Exist(int societeNo, int designationDocumentNo, string erpIntitule);
}
