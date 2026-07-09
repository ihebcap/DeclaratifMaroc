using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface ITresorerieMouvement : ITresorerieEntity
{
	int CaisseNo { get; }

	DateTime Date { get; }

	MouvementDomaine Domaine { get; }

	string Numero { get; }

	int SocieteNo { get; }
}
