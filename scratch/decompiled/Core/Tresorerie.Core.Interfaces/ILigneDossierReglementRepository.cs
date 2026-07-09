using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDossierReglementRepository
{
	IEnumerable<LigneDossierReglement> GetAll(int dossierNo);
}
