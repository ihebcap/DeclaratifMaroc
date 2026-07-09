using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IJoursReposRepository
{
	JoursRepos Get(int no);

	JoursRepos Get(int societeNo, DateTime date);

	List<JoursRepos> GetAll(int societeNo);

	int Create(JoursRepos joursRepos);

	void Update(JoursRepos joursRepos);

	void Delete(JoursRepos joursRepos);
}
