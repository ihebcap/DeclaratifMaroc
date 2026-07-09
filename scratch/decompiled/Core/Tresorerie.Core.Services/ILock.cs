using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public interface ILock
{
	Lock Get(TypeEntity entity, int entityNo, int societeNo);

	IEnumerable<Lock> GetAll();

	bool IsLocked(TypeEntity entity, int entityNo, int societeNo);

	bool IsLockedByMe(TypeEntity entity, int entityNo, int societeNo);

	bool IsLockedByOther(TypeEntity entity, int entityNo, int societeNo);

	bool Lock(TypeEntity entity, int entityNo, int societeNo, out int lockNo);

	bool Unlock(int lockNo);

	bool Unlock(TypeEntity entity, int entityNo, int societeNo);

	void Unlock();
}
