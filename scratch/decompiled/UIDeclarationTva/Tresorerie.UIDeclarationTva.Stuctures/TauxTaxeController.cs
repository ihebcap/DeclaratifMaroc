using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Models;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.LicenceGratuite;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class TauxTaxeController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpCommService _erpService;

	public TauxTaxeController(ILicenceApplicationVersion licenceApplicationVersion, IErpCommService erpService)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpService = erpService ?? throw new ArgumentNullException("erpService");
	}

	public List<IErpTaxe> GetAll()
	{
		return _erpService.GetAllTaxes().ToList();
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
