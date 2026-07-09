using System;

namespace Tresorerie.Core.Models;

public interface IErpExercice
{
	DateTime Debut { get; }

	DateTime Fin { get; }

	string Intitule { get; }

	bool IsCloture { get; }

	int Annee { get; set; }

	int No { get; }
}
