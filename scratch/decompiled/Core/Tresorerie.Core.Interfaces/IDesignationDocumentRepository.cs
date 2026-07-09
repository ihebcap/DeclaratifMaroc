using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDesignationDocumentRepository
{
	DesignationDocument Get(int no);

	DesignationDocument Get(string code);

	List<DesignationDocument> GetAll();

	bool IsUsed(int no);

	int? Create(DesignationDocument designationDocument);

	void Update(DesignationDocument designationDocument);

	void Delete(DesignationDocument designationDocument);
}
