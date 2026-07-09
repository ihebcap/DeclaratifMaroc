using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class LigneControleDelaisPaiementController
{
	private readonly EcheanceFournisseurHelper _echeanceHelper;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly ModeViewHelper _modeHelper;

	public LigneControleDelaisPaiementController(IGroupeService groupeService, EcheanceFournisseurHelper echeanceHelper, DeviseViewHelper deviseViewHelper, ModeViewHelper modeHelper)
	{
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_echeanceHelper = echeanceHelper ?? throw new ArgumentNullException("echeanceHelper");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_modeHelper = modeHelper ?? throw new ArgumentNullException("modeHelper");
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public List<ModeView> GetAllMode()
	{
		return _modeHelper.GetAll().ToList();
	}

	public void IntergerLigne(DeclarationDelaisPaiementView declarationView, IEnumerable<LigneControleDelaisPaiementView> views)
	{
		if (declarationView == null)
		{
			throw new ArgumentNullException("declarationView");
		}
		if (views == null)
		{
			throw new ArgumentNullException("views");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		foreach (LigneControleDelaisPaiementView view in views)
		{
			societeManager.DeclarationDelaisPaiementLigneAjouter(declarationView.No, view.EcheanceNo, view.AffectationNo, view.Depassement, view.EcheanceLegale);
		}
	}

	public List<LigneControleDelaisPaiementView> GetAll(LigneControleDelaisPaiementFiltreView filtreView)
	{
		if (filtreView == null)
		{
			throw new ArgumentNullException("filtreView");
		}
		if (filtreView.DateAu.Date < filtreView.DateDu.Date)
		{
			throw new ApplicationException("Période invalide.");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		_ = societeManager.Societe;
		DateTime dateDebut = filtreView.DateDu.Date;
		DateTime dateFin = filtreView.DateAu.Date;
		List<LigneControleDelaisPaiementView> list = new List<LigneControleDelaisPaiementView>();
		SocieteDevise deviseSociete = _deviseViewHelper.GetDefaultDeviseSociete();
		DateTime value = new DateTime(2023, 7, 1);
		DateTime dateLimiteMontant = new DateTime(2024, 12, 31);
		List<EcheanceView> source = (from x in _echeanceHelper.GetAllEcheances(value)
			where x.DeviseNo == deviseSociete.No && ((x.DocumentDate.Date <= dateLimiteMontant && x.Montant >= 10000m) || x.DocumentDate.Date > dateLimiteMontant)
			select x).ToList();
		List<EcheanceView> list2 = source.Where((EcheanceView x) => x.DatePevuePaiement.Date >= dateDebut && x.DatePevuePaiement.Date <= dateFin).ToList();
		List<EcheanceView> list3 = source.Where((EcheanceView x) => x.DatePevuePaiement.Date < dateDebut && x.Etat == Etat.NonPaye).ToList();
		List<EcheanceView> list4 = (from x in source
			where x.DatePevuePaiement.Date < dateDebut
			select (from z in societeManager.EcheanceGet(x.No).GetAffectations().ToList()
					.Select(delegate(Affectation a)
					{
						ReglementFournisseur reglementFournisseur3 = caisseManager.ReglementFournisseurGet(a.ReglementNo);
						if (reglementFournisseur3 == null)
						{
							throw new ApplicationException($"Impossible de charger le règlement de l'affectation [{a.No}].");
						}
						DateTime dateTime2 = ((reglementFournisseur3.Type == ReglementType.Cheque || reglementFournisseur3.Type == ReglementType.Traite || reglementFournisseur3.Type == ReglementType.Virement) ? reglementFournisseur3.DatePointage.Date : reglementFournisseur3.Date.Date);
						return (dateTime2 >= dateDebut && dateTime2 <= dateFin) ? a : null;
					})
				where z != null
				select z).Any() ? x : null into x
			where x != null
			select x).ToList();
		foreach (EcheanceView item in list3)
		{
			list.Add(new LigneControleDelaisPaiementView
			{
				EcheanceNo = item.No,
				EcheanceNumero = item.NumeroPiece,
				EcheanceCours = item.DeviseCours,
				EcheanceDateDocument = item.DocumentDate,
				EcheanceDatePrevue = item.DateEcheance,
				EcheanceDeviseNo = item.DeviseNo,
				EcheanceLegale = item.DatePevuePaiement,
				EcheanceMontant = item.Montant,
				EcheanceMontantDeviseSociete = item.MontantDevise,
				EcheanceSolde = item.Solde,
				EcheanceSoldeDeviseSociete = item.SoldeDevise,
				EcheanceDeviseCode = item.DeviseCode,
				Depassement = (int)(dateFin - dateDebut.GetMaxFrom(item.DatePevuePaiement)).TotalDays,
				TiersNo = item.ClientNo,
				TiersNumero = item.ClientNumero,
				TiersIntitule = item.ClientIntitule
			});
		}
		foreach (EcheanceView item2 in list4)
		{
			foreach (Affectation item3 in societeManager.EcheanceGet(item2.No).GetAffectations().ToList())
			{
				ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(item3.ReglementNo);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException($"Impossible de charger le règlement de l'affectation [{item3.No}].");
				}
				list.Add(new LigneControleDelaisPaiementView
				{
					EcheanceNo = item2.No,
					EcheanceNumero = item2.NumeroPiece,
					EcheanceCours = item2.DeviseCours,
					EcheanceDateDocument = item2.DocumentDate,
					EcheanceDatePrevue = item2.DateEcheance,
					EcheanceDeviseNo = item2.DeviseNo,
					EcheanceLegale = item2.DatePevuePaiement,
					EcheanceMontant = item2.Montant,
					EcheanceMontantDeviseSociete = item2.MontantDevise,
					EcheanceSolde = item2.Solde,
					EcheanceSoldeDeviseSociete = item2.SoldeDevise,
					EcheanceDeviseCode = item2.DeviseCode,
					DocumentInfoLibre1 = item2.Info1,
					DocumentInfoLibre2 = item2.Info2,
					DocumentInfoLibre3 = item2.Info3,
					DocumentInfoLibre4 = item2.Info4,
					Depassement = (int)(reglementFournisseur.DatePointage.Date - dateDebut).TotalDays,
					TiersNo = item2.ClientNo,
					TiersNumero = item2.ClientNumero,
					TiersIntitule = item2.ClientIntitule,
					ReglementNo = reglementFournisseur.No,
					AffectationMontant = item3.Montant,
					AffectationNo = item3.No,
					ReglementCours = reglementFournisseur.DeviseCours,
					ReglementDate = reglementFournisseur.Date,
					ReglementEcheance = reglementFournisseur.DateEcheance,
					ReglementMontant = reglementFournisseur.Montant,
					ReglementMontantDeviseSociete = reglementFournisseur.MontantDeviseSociete,
					ReglementNumero = reglementFournisseur.Numero,
					ReglementSolde = reglementFournisseur.Solde,
					ReglementSoldeDeviseSociete = reglementFournisseur.SoldeDevise,
					IsReglementComptabilise = (reglementFournisseur.IsComptabilise != EtatComptabilite.NonComptabilise),
					IsReglementPoint = reglementFournisseur.IsPointe,
					ReglementDatePointage = (reglementFournisseur.IsPointe ? new DateTime?(reglementFournisseur.DatePointage) : ((DateTime?)null)),
					ReglementInfoLibre1 = reglementFournisseur.Info1,
					ReglementInfoLibre2 = reglementFournisseur.Info2,
					ReglementInfoLibre3 = reglementFournisseur.Info3,
					ReglementInfoLibre4 = reglementFournisseur.Info4,
					ReglementModeNo = reglementFournisseur.ModeReglementNo,
					ReglementPieceNumero = reglementFournisseur.PieceNumero
				});
			}
		}
		foreach (EcheanceView item4 in list2)
		{
			foreach (Affectation item5 in societeManager.EcheanceGet(item4.No).GetAffectations().ToList())
			{
				ReglementFournisseur reglementFournisseur2 = caisseManager.ReglementFournisseurGet(item5.ReglementNo);
				if (reglementFournisseur2 == null)
				{
					throw new ApplicationException($"Impossible de charger le règlement de l'affectation [{item5.No}].");
				}
				bool flag = reglementFournisseur2.Type == ReglementType.Cheque || reglementFournisseur2.Type == ReglementType.Traite || reglementFournisseur2.Type == ReglementType.Virement;
				bool flag2 = (flag && (!reglementFournisseur2.IsPointe || reglementFournisseur2.DatePointage.Date > item4.DatePevuePaiement.Date)) || (!flag && reglementFournisseur2.Date.Date > item4.DatePevuePaiement.Date);
				if (reglementFournisseur2.IsComptabilise == EtatComptabilite.NonComptabilise || flag2)
				{
					DateTime dateTime = ((!flag) ? ((reglementFournisseur2.Date.Date > dateFin.Date) ? dateFin.Date : reglementFournisseur2.Date.Date) : ((!reglementFournisseur2.IsPointe) ? dateFin.Date : ((reglementFournisseur2.DatePointage.Date > dateFin.Date) ? dateFin.Date : reglementFournisseur2.DatePointage.Date)));
					if (!reglementFournisseur2.IsPointe)
					{
						_ = dateFin;
					}
					else if (!(reglementFournisseur2.DatePointage.Date > dateFin.Date))
					{
						_ = reglementFournisseur2.DatePointage.Date;
					}
					else
					{
						_ = dateFin;
					}
					list.Add(new LigneControleDelaisPaiementView
					{
						EcheanceNo = item4.No,
						EcheanceNumero = item4.NumeroPiece,
						EcheanceCours = item4.DeviseCours,
						EcheanceDateDocument = item4.DocumentDate,
						EcheanceDatePrevue = item4.DateEcheance,
						EcheanceDeviseNo = item4.DeviseNo,
						EcheanceLegale = item4.DatePevuePaiement,
						EcheanceMontant = item4.Montant,
						EcheanceMontantDeviseSociete = item4.MontantDevise,
						EcheanceSolde = item4.Solde,
						EcheanceSoldeDeviseSociete = item4.SoldeDevise,
						EcheanceDeviseCode = item4.DeviseCode,
						Depassement = (int)(dateTime - item4.DatePevuePaiement.Date).TotalDays,
						TiersNo = item4.ClientNo,
						TiersNumero = item4.ClientNumero,
						TiersIntitule = item4.ClientIntitule,
						ReglementNo = reglementFournisseur2.No,
						AffectationMontant = item5.Montant,
						AffectationNo = item5.No,
						ReglementCours = reglementFournisseur2.DeviseCours,
						ReglementDate = reglementFournisseur2.Date,
						ReglementEcheance = reglementFournisseur2.DateEcheance,
						ReglementMontant = reglementFournisseur2.Montant,
						ReglementMontantDeviseSociete = reglementFournisseur2.MontantDeviseSociete,
						ReglementNumero = reglementFournisseur2.Numero,
						ReglementSolde = reglementFournisseur2.Solde,
						ReglementSoldeDeviseSociete = reglementFournisseur2.SoldeDevise,
						IsReglementComptabilise = (reglementFournisseur2.IsComptabilise != EtatComptabilite.NonComptabilise),
						IsReglementPoint = reglementFournisseur2.IsPointe,
						ReglementDatePointage = (reglementFournisseur2.IsPointe ? new DateTime?(reglementFournisseur2.DatePointage) : ((DateTime?)null)),
						ReglementInfoLibre1 = reglementFournisseur2.Info1,
						ReglementInfoLibre2 = reglementFournisseur2.Info2,
						ReglementInfoLibre3 = reglementFournisseur2.Info3,
						ReglementInfoLibre4 = reglementFournisseur2.Info4,
						ReglementModeNo = reglementFournisseur2.ModeReglementNo,
						ReglementPieceNumero = reglementFournisseur2.PieceNumero
					});
				}
			}
			if (item4.Etat == Etat.NonPaye)
			{
				list.Add(new LigneControleDelaisPaiementView
				{
					EcheanceNo = item4.No,
					EcheanceNumero = item4.NumeroPiece,
					EcheanceCours = item4.DeviseCours,
					EcheanceDateDocument = item4.DocumentDate,
					EcheanceDatePrevue = item4.DateEcheance,
					EcheanceDeviseNo = item4.DeviseNo,
					EcheanceLegale = item4.DatePevuePaiement,
					EcheanceMontant = item4.Montant,
					EcheanceMontantDeviseSociete = item4.MontantDevise,
					EcheanceSolde = item4.Solde,
					EcheanceSoldeDeviseSociete = item4.SoldeDevise,
					EcheanceDeviseCode = item4.DeviseCode,
					Depassement = (int)(dateFin - item4.DatePevuePaiement.Date).TotalDays,
					TiersNo = item4.ClientNo,
					TiersNumero = item4.ClientNumero,
					TiersIntitule = item4.ClientIntitule
				});
			}
		}
		return list.Where((LigneControleDelaisPaiementView x) => x.Depassement > 0).ToList();
	}
}
