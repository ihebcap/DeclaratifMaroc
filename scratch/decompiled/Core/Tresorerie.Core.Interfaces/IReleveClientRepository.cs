using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IReleveClientRepository
{
	Task<IEnumerable<ReleveClient>> GetAllAsync(int societeNo, string clientCode, bool releveBL, DateTime dateLimitBL);
}
