using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;
using Tresorerie.UICommun.LicenceGratuite;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.Controllers;

public class DeclarationRetenuSourceController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IErpCommService _erpCommService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly BanqueViewHelper _banqueHelper;

	private readonly FournisseurErpHelper _fournisseurHelper;

	private readonly ClientErpHelper _clientHelper;

	public DeclarationRetenuSourceController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IErpCommService erpCommService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper, FournisseurErpHelper fournisseurHelper, ClientErpHelper clientHelper, BanqueViewHelper banqueHelper)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_erpCommService = erpCommService ?? throw new ArgumentNullException("erpCommService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_fournisseurHelper = fournisseurHelper ?? throw new ArgumentNullException("fournisseurHelper");
		_clientHelper = clientHelper ?? throw new ArgumentNullException("clientHelper");
		_banqueHelper = banqueHelper ?? throw new ArgumentNullException("banqueHelper");
	}

	public List<CodeActivite> GetAllCodeActivite()
	{
		return _groupeService.CodeActiviteManager.GetAll();
	}

	public IList<IErpTaxe> GetAllErpTaxe()
	{
		return _erpCommService.GetAllTaxes().ToList();
	}

	public List<IErpFournisseur> GetAllFournisseur()
	{
		return (from x in _fournisseurHelper.GetAll(reload: true)
			where !x.EnSommeil
			select x).ToList();
	}

	public IEnumerable<ModeView> GetAllModeReglement()
	{
		return from mode in _groupeService.SocieteManager.Societe.GetAllModes()
			select new ModeView
			{
				No = mode.No,
				Type = mode.Type,
				Code = mode.Code,
				Designation = mode.Intitule
			};
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public int GetNombreDecimalDefaultDevise()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).NombreDecimales;
	}

	public List<IErpExercice> GetAllExercice()
	{
		return (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			select x).ToList();
	}

	public void AuthorisationExport()
	{
	}

	public bool IsLicenceGratuit()
	{
		try
		{
			_licenceApplicationVersion.ThrowIfGratuit();
			return true;
		}
		catch (Exception ex)
		{
			new FrmMessageBoxLicenceGratuite(ex.Message).ShowDialog();
			return false;
		}
	}
}
