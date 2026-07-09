using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Helper;
using Tresorerie.UIDeclarationTva.Stuctures;

namespace Tresorerie.UIDeclarationTva.ImportService;

public class LigneDeclarationTvaEncaissementImportService
{
	private readonly ILigneDeclarationTvaEncaissementImportRepository _repository;

	private readonly IGroupeService _groupe;

	private readonly FournisseurErpHelper _fournisseurHelper;

	private readonly ClientErpHelper _clientHelper;

	private readonly IErpCommService _erpService;

	public LigneDeclarationTvaEncaissementImportService(ILigneDeclarationTvaEncaissementImportRepository repository, IGroupeService groupe, FournisseurErpHelper fournisseurHelper, ClientErpHelper clientHelper, IErpCommService erpService)
	{
		_repository = repository ?? throw new ArgumentNullException("repository");
		_groupe = groupe ?? throw new ArgumentNullException("groupe");
		_fournisseurHelper = fournisseurHelper ?? throw new ArgumentNullException("fournisseurHelper");
		_clientHelper = clientHelper ?? throw new ArgumentNullException("clientHelper");
		_erpService = erpService ?? throw new ArgumentNullException("erpService");
	}

	public void ImporterLigneDeclaration(DeclarationTvaEncaissementView declaration, string source, IProgress<int> progress, CancellationToken cancellationToken)
	{
		if (declaration == null)
		{
			throw new ApplicationException("declaration");
		}
		if (string.IsNullOrEmpty(source))
		{
			throw new ArgumentNullException("source");
		}
		if (declaration.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		SocieteManager societeManager = _groupe.SocieteManager;
		_ = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
		}
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'identifiant du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameNatureFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à la nature du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameCodeActiviteMarroc))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant au code activité du tiers.");
		}
		IEnumerable<IErpTiersIce> allIceTiersToMaroc = _erpService.GetAllIceTiersToMaroc(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur, societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur, societe.ErpColumnNameNatureFournisseur, societe.ErpColumnNameCodeActiviteMarroc, societe.ErpColumnValueNumRegistreCommerceFournisseur);
		IEnumerable<IErpTaxe> allTaxes = _erpService.GetAllTaxes();
		List<CodeActivite> all = _groupe.CodeActiviteManager.GetAll();
		List<LigneDeclarationTvaEncaissementImport> list = _repository.GetAll(source).ToList();
		Verify(declaration, list, cancellationToken);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		decimal num = default(decimal);
		foreach (LigneDeclarationTvaEncaissementImport lg in list)
		{
			cancellationToken.ThrowIfCancellationRequested();
			progress.Report((int)(num++ / (decimal)list.Count() * 100m));
			string text = "";
			IErpFournisseur erpFournisseur = _fournisseurHelper.Get(lg.TiersCode);
			text = ((erpFournisseur != null) ? erpFournisseur.Intitule : (_clientHelper.Get(lg.TiersCode) ?? throw new ApplicationException($"Impossible de charger le tiers {lg.TiersCode}.[lg:{num}]")).Intitule);
			IErpTiersIce erpTiersIce = allIceTiersToMaroc.SingleOrDefault((IErpTiersIce x) => x.TiersCode == lg.TiersCode);
			if (erpTiersIce == null)
			{
				throw new ApplicationException($"Impossible de charger les infos ICE du tiers [{lg.TiersCode}].[lg:{num}]");
			}
			IErpTaxe erpTaxe = allTaxes.SingleOrDefault((IErpTaxe x) => x.Code == lg.ErpTaxeCode);
			if (erpTaxe == null)
			{
				throw new ApplicationException($"Impossible de détermier la taxe [{lg.ErpTaxeCode}].[lg:{num}]");
			}
			SocieteCodeActiviteTaxe societeCodeActiviteTaxe = societe.GetSocieteCodeActivite().Single((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == lg.ErpTaxeCode);
			if (societeCodeActiviteTaxe == null)
			{
				throw new ApplicationException($"Impossible de détermier le correspondance code activité de la taxe [{lg.ErpTaxeCode}].[lg:{num}]");
			}
			CodeActivite codeActivite = all.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe.CodeActiviteNo);
			if (codeActivite == null)
			{
				throw new ApplicationException($"Impossible de charger le code activité.[lg:{num}]");
			}
			societeManager.DeclarationTvaEncaissementLigneAjouter(declaration.No, null, LigneDeclarationTvaEncaissementEntityType.Manuelle, LigneDeclarationTvaEncaissementIntegrationType.Importation, lg.Domaine, lg.Assiette, erpTaxe.Taux, lg.Montant, lg.TiersCode, text, erpTiersIce.TiersIdentifiant, erpTiersIce.TiersIce, lg.MouvementNumero, lg.DocumentNumero, lg.TypePayement, lg.DateMouvement, lg.DateDocument, lg.ErpTaxeCode, codeActivite.Code, lg.Prorata, lg.DesignationDocument);
		}
		transactionScope.Complete();
	}

	private void Verify(DeclarationTvaEncaissementView declaration, IEnumerable<LigneDeclarationTvaEncaissementImport> lignesImport, CancellationToken cancellationToken)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		if (lignesImport == null)
		{
			throw new ArgumentNullException("lignesImport");
		}
		if (declaration.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		SocieteManager societeManager = _groupe.SocieteManager;
		_ = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
		}
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'identifiant du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameNatureFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à la nature du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameCodeActiviteMarroc))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant au code activité du tiers.");
		}
		IEnumerable<IErpTiersIce> allIceTiersToMaroc = _erpService.GetAllIceTiersToMaroc(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur, societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur, societe.ErpColumnNameNatureFournisseur, societe.ErpColumnNameCodeActiviteMarroc, societe.ErpColumnValueNumRegistreCommerceFournisseur);
		IEnumerable<IErpTaxe> allTaxes = _erpService.GetAllTaxes();
		List<CodeActivite> all = _groupe.CodeActiviteManager.GetAll();
		int num = 1;
		foreach (LigneDeclarationTvaEncaissementImport lg in lignesImport)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(lg.TiersCode))
			{
				throw new ApplicationException($"Le code tiers est obligatoire. [lg:{num}]");
			}
			if (_fournisseurHelper.Get(lg.TiersCode) == null && _clientHelper.Get(lg.TiersCode) == null)
			{
				throw new ApplicationException($"Impossible de charger le tiers {lg.TiersCode}. [lg:{num}]");
			}
			if (allIceTiersToMaroc.SingleOrDefault((IErpTiersIce x) => x.TiersCode == lg.TiersCode) == null)
			{
				throw new ApplicationException($"Impossible de charger les infos ICE du tiers [{lg.TiersCode}]. [lg:{num}]");
			}
			if (allTaxes.SingleOrDefault((IErpTaxe x) => x.Code == lg.ErpTaxeCode) == null)
			{
				throw new ApplicationException($"Impossible de détermier la taxe [{lg.ErpTaxeCode}]. [lg:{num}]");
			}
			SocieteCodeActiviteTaxe societeCodeActiviteTaxe = societe.GetSocieteCodeActivite().Single((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == lg.ErpTaxeCode);
			if (societeCodeActiviteTaxe == null)
			{
				throw new ApplicationException($"Impossible de détermier le correspondance code activité de la taxe [{lg.ErpTaxeCode}]. [lg:{num}]");
			}
			if (all.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe.CodeActiviteNo) == null)
			{
				throw new ApplicationException($"Impossible de charger le code activité. [lg:{num}]");
			}
			if (lg.DateMouvement < declaration.DateDebut || lg.DateMouvement > declaration.DateFin)
			{
				throw new ApplicationException($"Date hors période de déclaration. [lg:{num}]");
			}
			if (string.IsNullOrEmpty(lg.DocumentNumero))
			{
				throw new ApplicationException($"Le numéro de la pièce est obligatoire. [lg:{num}]");
			}
			if (string.IsNullOrEmpty(lg.DesignationDocument))
			{
				throw new ApplicationException($"La désignation de la pièce est obligatoire. [lg:{num}]");
			}
			if (string.IsNullOrEmpty(lg.MouvementNumero))
			{
				throw new ApplicationException($"Le numéro mouvement est obligatoire. [lg:{num}]");
			}
			if (lg.Assiette == 0m)
			{
				throw new ApplicationException($"Assiette invalide. [lg:{num}]");
			}
			if (lg.Montant == 0m)
			{
				throw new ApplicationException($"Montant invalide. [lg:{num}]");
			}
			if (lg.Prorata < 0m || lg.Prorata > 100m)
			{
				throw new ApplicationException($"Prorata invalide. [lg:{num}]");
			}
			num++;
		}
	}
}
