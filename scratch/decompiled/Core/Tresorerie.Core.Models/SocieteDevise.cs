using System;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class SocieteDevise : Devise
{
	public int ErpNo { get; set; }

	public int SocieteNo { get; private set; }

	public SocieteDevise(int societeNo, Devise devise, int deviseErpNo)
		: base(devise.No, devise.Code, devise.Intitule)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo, "societeNo");
		}
		if (deviseErpNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseErpInvalide, "deviseErpNo");
		}
		base.Monnaie = devise.Monnaie;
		base.Sigle = devise.Sigle;
		base.SousMonnaie = devise.SousMonnaie;
		base.Cours = devise.Cours;
		base.Format = devise.Format;
		base.Libor = devise.Libor;
		base.Code = devise.Code;
		SocieteNo = societeNo;
		ErpNo = deviseErpNo;
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoSocieteDevise, SocieteNo, base.No);
	}
}
