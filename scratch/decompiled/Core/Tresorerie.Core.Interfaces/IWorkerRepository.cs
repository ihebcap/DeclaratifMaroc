using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IWorkerRepository
{
	Worker GetFirst(int societeNo, TypeEntity typeEntity, ErpDomaine domaine);

	Worker Get(int no);

	void Delete(int no);

	void SetLoked(int no, int userNo);

	int Create(Worker worker);

	bool Existe(int societeNo, ErpDomaine domaine, TypeEntity typeEntity, int entityNo);

	bool Existe(int societeNo, ErpDomaine domaine, TypeEntity typeEntity, int entityNo, string entityNumero);

	bool Existe(int societeNo, ErpDomaine domaine, TypeEntity typeEntity, string entityNumero, ErpDocumentType documentType);

	void DeleteAllLocked(int societeNo, int frequence, TypeEntity typeEntity, ErpDomaine domaine);

	void DeleteAllLockedByUser(int societeNo, int userNo, TypeEntity typeEntity, ErpDomaine domaine);

	IEnumerable<Worker> GetALLNonLocked(TypeEntity typeEntity, ErpDomaine domaine, int societeNo);
}
