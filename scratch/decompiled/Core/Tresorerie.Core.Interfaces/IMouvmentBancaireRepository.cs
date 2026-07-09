using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvmentBancaireRepository
{
	void EscompteRegler(int mouvementNo, bool isPointe, DateTime datePointage);

	IEnumerable<MouvementBancaire> GetAll(int banqueNo, int societeNo, DateTime dateMin, DateTime dateMax, bool isPointer, int deviseSocieteNo, List<TypeBordereau> typeBordereauChequeTraite, List<InformationsBanqueBordereau> infoBanqueTypeChequeTraite);

	bool HasMouvementBancaireNonRapproche(int banqueNo, int societeNo, DateTime dateMax, int deviseSocieteNo);

	void PointerMouvement(int mouvementNo, bool isPointe, DateTime datePointage, string extraitNum);

	void PointerRemboursement(int mouvementNo, bool isPointe, DateTime datePointage, string extraitNum);

	void PointerVirementInterne(int mouvementNo, bool isPointe, DateTime datePointage, string extraitNum);

	IEnumerable<MouvementBancaire> GetAllMouvementBancaire(int societeNo);

	List<MouvementBancaire> GetAllReglementClientByBordreauNumero(int societeNo, string bordereauNumero);
}
