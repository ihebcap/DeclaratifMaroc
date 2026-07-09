using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IUserPasswordRepository
{
	int? Create(UserPassword userPassword);

	UserPassword Get(int id);

	IEnumerable<UserPassword> GetAll(int userId);

	UserPassword GetLastUserPassword(int userId);
}
