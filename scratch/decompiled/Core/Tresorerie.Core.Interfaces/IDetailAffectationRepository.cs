using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDetailAffectationRepository
{
	IEnumerable<DetailAffectation> GetByClient(int societeNo, int tiersNO = 0, int RepresentantNo = 0, DateTime? dateDebut = null, DateTime? dateFin = null);

	IEnumerable<DetailAffectation> GetByFournisseur(int societeNo, int tiersNO = 0, int representantNo = 0, DateTime? dateDebut = null, DateTime? dateFin = null);

	DetailAffectation GetToSynchroniser(int affectationNo, int societeNo);

	IEnumerable<DetailAffectation> GetAll(int societeNo, ErpDomaine domaine, bool isSynchroniser, DateTime? dateDebut = null, DateTime? dateFin = null, params EcheanceType[] echeanceType);

	IEnumerable<DetailAffectation> GetAllByReglement(int societeNo, int reglementNo, ErpDomaine domaine, bool isSynchroniser, params EcheanceType[] echeanceType);
}
