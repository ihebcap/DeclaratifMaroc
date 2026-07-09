using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvementCaisseEspeceRepository
{
	IEnumerable<MouvementCaisseEspece> GetAll(int[] caisseNo, int deviseNo, DateTime dateDu, DateTime dateA, bool useDateCreation);

	IEnumerable<LotSoldeCaisseEspece> GetAllLot(int[] caisseNo, int deviseNo, DateTime dateDu, DateTime dateA, bool useDateCreation);
}
