using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICodeActiviteRepository
{
	CodeActivite Get(int no);

	CodeActivite Get(string code);

	List<CodeActivite> GetAll();

	bool IsUsed(int no);

	int? Create(CodeActivite codeActivite);

	void Update(CodeActivite codeActivite);

	void Delete(CodeActivite codeActivite);
}
