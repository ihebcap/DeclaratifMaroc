using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.LicenceGratuite;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class BalanceController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	public BalanceController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
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

	public List<IErpBalance> GetBalance(DateTime dateDebut, DateTime dateFin)
	{
		return _erpComptaService.GetBalance(dateDebut, dateFin).ToList();
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

	public List<IErpComptaEcritureComptable> GetGrandLivre(IErpBalance balance, DateTime dateDebut, DateTime dateFin)
	{
		if (balance == null)
		{
			throw new ArgumentNullException("balance");
		}
		return _erpComptaService.GetEcritures(dateDebut, dateFin, balance.CompteNumero).ToList();
	}
}
