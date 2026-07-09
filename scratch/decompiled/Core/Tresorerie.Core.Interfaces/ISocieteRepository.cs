using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteRepository
{
	int? Create(Societe societe);

	void Delete(Societe socite);

	Societe Get(int no);

	Societe Get(string raisonSocial);

	IEnumerable<Societe> GetAll();

	IEnumerable<Societe> GetAllByVisibilite(bool visibilite);

	string GetNumeroPieceCourante(int societeNo, EntityNumerotation numerotation);

	string GetNumeroPieceCourante(string prefix, bool isAnneeEnable, bool isMoisEnable, int count, EntityNumerotation numerotation, int? societeNo);

	void Update(Societe societe);

	decimal GetDelaisMoyenPaiement(int no, ErpDomaine domaine, DateTime dateDu, DateTime dateAu);

	Dictionary<DateTime, decimal> GetDetailsDelaisMoyenPaiement(int no, ErpDomaine domaine, DateTime dateDu, DateTime dateAu);
}
