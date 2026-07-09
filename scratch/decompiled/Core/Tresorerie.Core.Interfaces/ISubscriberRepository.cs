using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;

namespace Tresorerie.Core.Interfaces;

public interface ISubscriberRepository
{
	bool Create(Guid id, int societeNo, int utilisateurNo, string utilisateurLogin, ApplicationRunning applicationRunning);

	bool Delete(Guid id);

	void DeleteDeadSubscribers();

	IEnumerable<Subscriber> GetAll();

	IEnumerable<Subscriber> GetAllWithoutApplication();

	bool HasConnectedSession();

	bool HasConnectedSession(Guid subscriberId);

	bool IsConnectedLogin(int utilisateurNo, string utilisateurLogin, ApplicationRunning applicationRunning);

	bool Update(Guid id, int societeNo);
}
