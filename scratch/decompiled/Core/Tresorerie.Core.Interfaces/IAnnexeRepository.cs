using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAnnexeRepository
{
	int? Create(Annexe annexe);

	void Delete(Annexe annexe);

	Annexe Get(int no);

	Annexe Get(string code);

	IEnumerable<Annexe> GetAll();

	bool IsAnnexeUsed(int annexeNo);

	void Update(Annexe annexe);
}
