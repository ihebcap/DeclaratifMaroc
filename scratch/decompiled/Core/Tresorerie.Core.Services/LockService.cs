using System;
using System.Collections.Generic;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class LockService : ILock
{
	private readonly ILockRepository _lockRepository;

	private readonly ISubscriberRepository _subscriberRepository;

	public LockService(ILockRepository lockRepository, ISubscriberRepository subscriberRepository)
	{
		_lockRepository = lockRepository ?? throw new ArgumentNullException("lockRepository");
		_subscriberRepository = subscriberRepository ?? throw new ArgumentNullException("subscriberRepository");
	}

	public Lock Get(TypeEntity entity, int entityNo, int societeNo)
	{
		return _lockRepository.Get(entity, entityNo, societeNo);
	}

	public IEnumerable<Lock> GetAll()
	{
		return _lockRepository.GetAll();
	}

	public bool IsLocked(TypeEntity entity, int entityNo, int societeNo)
	{
		return _lockRepository.IsLocked(entity, entityNo, societeNo);
	}

	public bool IsLockedByMe(TypeEntity entity, int entityNo, int societeNo)
	{
		return _lockRepository.IsLockedByMe(entity, entityNo, societeNo);
	}

	public bool IsLockedByOther(TypeEntity entity, int entityNo, int societeNo)
	{
		return _lockRepository.IsLockedByOther(entity, entityNo, societeNo);
	}

	public bool Lock(TypeEntity entity, int entityNo, int societeNo, out int lockNo)
	{
		if (!_subscriberRepository.HasConnectedSession(NotifyService.Guid))
		{
			throw new ApplicationException("Session utilisateur invalide ! Veuillez se reconnecter .");
		}
		return _lockRepository.Lock(entity, entityNo, societeNo, out lockNo);
	}

	public bool Unlock(int lockNo)
	{
		return _lockRepository.Unlock(lockNo);
	}

	public bool Unlock(TypeEntity entity, int entityNo, int societeNo)
	{
		return _lockRepository.Unlock(entity, entityNo, societeNo);
	}

	public void Unlock()
	{
		_lockRepository.Unlock();
	}
}
