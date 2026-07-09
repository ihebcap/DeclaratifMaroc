using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IPrevisionnelleRepository
{
	void AnnulerPrevision(int no, bool isAnnuler);

	void ChangerBanquePrevue(int no, int banqueNo);

	void DecalerEcheancePrevue(int no, DateTime date);

	void Delete(int no);

	Previsionnelle Get(int no);

	Task<IEnumerable<Previsionnelle>> GetAllAsync(DateTime dateMax, params int[] societesNo);

	IEnumerable<OperationBancaire> GetAllOperationBancaire(int societeNo);

	IEnumerable<OperationBancaire> GetAllOperationBancaire(int societeNo, string dossierNumero);

	IEnumerable<OperationBancaire> GetAllOperationBancaire(int societeNo, DateTime dateDe, DateTime dateA, bool isComptabilise, CancellationToken cancellationToken);

	IEnumerable<OperationBancaire> GetAllOperationBancaireToDeclaration(int societeNo, DateTime dateDe, DateTime dateA, CancellationToken cancellationToken);

	IEnumerable<Prevision> GetAllPrevision(int societeNo);

	OperationBancaire GetOperationBancaire(int no);

	OperationBancaire GetOperationBancaireByBordereau(int bordereauNo);

	Prevision GetPrevision(int no);

	int Insert(OperationBancaire operation);

	void OperationBancaireUpdateCompta(int no, bool compta, DateTime dateCompta);

	void OperationBancaireSetDeclarationTva(int no, int? declarationNo);

	void Insert(Prevision prevision);

	Task MiseAJourPrevisionnelleAsync(int societeNo, bool integrerEcheanceClient, bool integrerEcheanceFournisseur, bool integrerEcrituresFournisseur, TypeIntegrationPrevisionnelleClient integrerBcBlFaClient, PrevisionnelStatutPiece previsionnelStatutPieceClient, TypeIntegrationPrevisionnelleFournisseur integrerBcBlFaFournisseur, PrevisionnelStatutPiece previsionnelStatutPieceFournisseur, bool hasStatutSaisie, bool hasStatutConfirme);

	void PointerMouvement(int no, bool isPointe, DateTime datePointe);

	void Update(Prevision prevision);

	void UpdateCoursPrevisionnel(Previsionnelle previsionnelle);
}
