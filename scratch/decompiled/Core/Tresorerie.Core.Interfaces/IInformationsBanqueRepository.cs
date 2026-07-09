using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IInformationsBanqueRepository
{
	int Create(InformationsBanque informationsBanque);

	void Delete(InformationsBanque informationsBanque);

	IEnumerable<InformationsBanque> GetAll();

	IEnumerable<InformationsBanque> GetAll(int societeNo);

	InformationsBanque Get(int no);

	InformationsBanque GetDefaultBanquePrevisionnel(int societeNo);

	InformationsBanque GetByErpNo(int societeNo, int erpNo);

	void Update(InformationsBanque informationsBanque);

	int Create(InformationsBanqueBordereau banqueBordereau);

	void DeleteLigne(InformationsBanqueBordereau banqueBordereau);

	void DeleteLigne(int informationsBanqueNo);

	InformationsBanqueBordereau GetLigne(int no);

	IEnumerable<InformationsBanqueBordereau> GetAllLignes(int informationsBanqueNo);
}
