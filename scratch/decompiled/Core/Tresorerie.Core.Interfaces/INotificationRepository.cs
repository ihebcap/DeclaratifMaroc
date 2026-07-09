using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface INotificationRepository
{
	bool Create(Guid subscriberId, TypeEntity entity, TypeAction action, int id, int societeNo);

	bool Delete(int id);

	IEnumerable<Notification> GetAll(Guid subscriberId);
}
