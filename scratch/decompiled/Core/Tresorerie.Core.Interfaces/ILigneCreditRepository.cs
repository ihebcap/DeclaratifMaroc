using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneCreditRepository
{
	LigneCredit Get(int no);

	IEnumerable<LigneCredit> GetAll(int creditNo);

	int Create(LigneCredit ligne);

	void Delete(int no);

	void DeleteFromCredit(int creditNo);

	void UpdateEcheance(int no, DateTime dateEcheance, string numero);

	void SetPointer(LigneCredit ligne);

	void UpdateTauxInteretLigneCredit(LigneCredit ligne);

	void Update(LigneCredit ligne);

	void UpdateNumero(int no, string numero);

	IEnumerable<LigneCredit> GetAllBySocieteNo(int societeNo);
}
