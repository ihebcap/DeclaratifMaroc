using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IHistoriqueMvtRepository
{
	int Create(HistoriqueMvt historiqueMvt);

	void Delete(HistoriqueMvt historiqueMvt);

	HistoriqueMvt Get(int no);

	IEnumerable<HistoriqueMvt> GetAllByCaisse(int caisseNo, int deviseNo);

	IEnumerable<HistoriqueMvt> GetAllByCaisse(int[] caisseNo, int deviseNo);

	IEnumerable<HistoriqueMvt> GetAllByMouvement(int lotNo);

	IEnumerable<HistoriqueMvt> GetAllLigneBordreau(int bodereauNo);

	IEnumerable<HistoriqueMvt> GetAllLignesEntreeTransfert(int transfertNo);

	IEnumerable<HistoriqueMvt> GetAllLignesTransfert(int transfertNo);

	IEnumerable<HistoriqueMvt> GetAllNonEpuise(int caisseNo, int modeNo, int deviseNo);

	IEnumerable<HistoriqueMvt> GetAllNonEpuise(int[] caisseNo, int[] modeNo, int deviseNo);

	HistoriqueMvt GetLigneSortieTransfert(int transfertNo, int mouvementNo, int modeNo);

	HistoriqueMvt GetNonEpuise(int caisseNo, int modeNo, int deviseNo, int lotNo);

	IEnumerable<HistoriqueMvt> GetReglementFournisseurEspece(int reglementNo);

	void Update(HistoriqueMvt historiqueMvt);
}
