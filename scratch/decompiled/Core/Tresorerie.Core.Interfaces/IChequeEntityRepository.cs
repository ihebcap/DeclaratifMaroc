using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IChequeEntityRepository
{
	IEnumerable<ChequeEntity> GetAll(int societeNo, int chequeNo);
}
