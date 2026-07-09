using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IBanqueTiersRepository
{
	bool ExistMouvement(BanqueTiers banqueTiers);

	IEnumerable<BanqueTiers> GetAll(string tiersNum, int societeNo);

	IEnumerable<BanqueTiers> GetAll(int societeNo);

	IEnumerable<int> GetAllByPays(int paysNo);

	BanqueTiers Get(int no);

	BanqueTiers Get(string banque, string tiersNum, int societeNo);

	int Create(BanqueTiers banqueTiers);

	void Update(BanqueTiers banqueTiers);

	void UpdateIsPrincipal(BanqueTiers banqueTiers);

	void Delete(int no);
}
