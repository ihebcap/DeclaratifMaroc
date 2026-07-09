using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IChequeRepository
{
	int? Create(Cheque cheque);

	void Delete(Cheque cheque);

	Cheque Get(int no);

	Cheque Get(string numero, int cheqierNo, int societeNo);

	Cheque Get(string numero, int societeNo);

	IEnumerable<Cheque> GetAll(int chequierNo);

	IEnumerable<Cheque> GetAll(int chequierNo, int societeNo, params ChequeStatut[] statuts);

	void Reutiliser(int chequeNo);

	void Update(Cheque cheque);

	void UpdatePlafond(int chequeNo, decimal montant);
}
