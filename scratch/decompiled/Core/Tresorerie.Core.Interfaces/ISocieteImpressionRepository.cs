using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteImpressionRepository
{
	void Create(SocieteEtat societeImpression);

	void Delete(SocieteEtat etat);

	SocieteEtat EtatGetStandard(int source, string name, int societeNo);

	SocieteEtat EtatGetStandard(string name, int societeNo);

	SocieteEtat Get(int id);

	IEnumerable<SocieteEtat> GetAll();

	IEnumerable<SocieteEtat> GetAll(int societeNo);

	IEnumerable<SocieteEtat> GetBySource(int no, int societeNo);

	SocieteEtat GetExistEtatBord(int bordereauTypeNo, int banqueNo, TypeRapport type, int societeNo);

	void Update(SocieteEtat societeImpression);
}
