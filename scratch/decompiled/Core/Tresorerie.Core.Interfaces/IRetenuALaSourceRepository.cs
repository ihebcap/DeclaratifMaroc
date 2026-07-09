using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRetenuALaSourceRepository
{
	RetenuALaSource Get(int no);

	RetenuALaSource Get(string numero, int societeNo);

	IEnumerable<RetenuALaSource> GetAll(int societeNo);

	IEnumerable<RetenuALaSource> GetAllToDeclarationRAS(int societeNo, DateTime dateDu, DateTime dateAu);

	IEnumerable<RetenuALaSource> GetAll(int tiersNo, int societeNo);

	IEnumerable<RetenuALaSource> GetAllByDossier(int dossierNo);

	int? Create(RetenuALaSource retenue);

	void Update(int no, bool isImprime, DateTime dateImpression, int modificateurNo);

	void UpdateDossier(int no, int? dossierNo, string dossierNumero, int modificateurNo);

	IEnumerable<RetenuALaSource> GetAllNonSolde(int societeNo, int tiersNo, int caisseNo, ErpDomaine domaine);

	void UpdateCours(RetenuALaSource retenu);

	bool EcheanceHasRetenue(int echeanceNo);

	bool EcheanceHasRetenue(int echeanceNo, int dossierNo);

	IEnumerable<RetenuALaSource> GetAllByEcheance(int echeanceNo);

	IEnumerable<RetenuALaSource> GetByDeclarationTej(int declarationNo);

	IEnumerable<RetenuALaSource> GetByPeriode(int societeNo, DateTime dateDebut, DateTime dateFin);

	void SetAsDeclarerTej(int no, int declarationNo, string declarationNum, int modificateurNo);

	void DeleteDeclarationTej(int no, int modificateurNo);

	bool CanAddRetenue(int societeNo, DateTime date);

	bool DeclarationTejHasLignes(int declarationNo);

	void Update(int no, string info1, string info2, string info3, string info4);
}
