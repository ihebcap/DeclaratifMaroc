using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;

namespace Tresorerie.Core.Services;

public class NotifyService
{
	private readonly INotificationRepository _notificationRepository;

	private readonly ISubscriberRepository _subscriberRepository;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	public static Guid Guid { get; private set; }

	static NotifyService()
	{
		Guid = Guid.NewGuid();
	}

	public NotifyService(INotificationRepository notificationRepository, ISubscriberRepository subscriberRepository, ILicenceApplicationVersion licenceApplicationVersion)
	{
		_notificationRepository = notificationRepository ?? throw new ArgumentNullException("notificationRepository");
		_subscriberRepository = subscriberRepository ?? throw new ArgumentNullException("subscriberRepository");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
	}

	public bool ChangeSociete(int societeNo)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = _subscriberRepository.Update(Guid, societeNo);
		transactionScope.Complete();
		return result;
	}

	public bool Delete(int notificationId)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = _notificationRepository.Delete(notificationId);
		transactionScope.Complete();
		return result;
	}

	public IEnumerable<Notification> GetAll()
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
		{
			_subscriberRepository.DeleteDeadSubscribers();
			transactionScope.Complete();
		}
		return _notificationRepository.GetAll(Guid);
	}

	public bool Notify(TypeEntity entity, int no, TypeAction action, int societeNo)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = (from x in _subscriberRepository.GetAll()
			where x.SocieteNo == societeNo
			select new Notification
			{
				Entity = entity,
				Action = action,
				EntityId = no,
				SubscriberId = x.Id
			}).All((Notification x) => _notificationRepository.Create(x.SubscriberId, entity, action, no, societeNo));
		transactionScope.Complete();
		return result;
	}

	public bool NotifyNone()
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = (from x in _subscriberRepository.GetAll()
			where x.Id != Guid
			select x).All((Subscriber x) => _notificationRepository.Create(x.Id, TypeEntity.None, TypeAction.None, 0, x.SocieteNo));
		transactionScope.Complete();
		return result;
	}

	public bool Subscribe(int societeNo, int utilisateurNo, string login)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = _subscriberRepository.Create(Guid, societeNo, utilisateurNo, login, _licenceApplicationVersion.Application);
		transactionScope.Complete();
		return result;
	}

	public bool Unsubscribe()
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bool result = _subscriberRepository.Delete(Guid);
		transactionScope.Complete();
		return result;
	}

	public bool HasConnectedSession()
	{
		return _subscriberRepository.HasConnectedSession(Guid);
	}

	public bool IsConnectedLogin(int utilisateurNo, string utilisateurLogin)
	{
		return _subscriberRepository.IsConnectedLogin(utilisateurNo, utilisateurLogin, _licenceApplicationVersion.Application);
	}
}
