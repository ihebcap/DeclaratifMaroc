using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class DeviseManager
{
	private readonly NotifyService _notifyService;

	private readonly IDeviseRepository _repository;

	public GroupeService Groupe { get; set; }

	public DeviseManager(IDeviseRepository deviseRepository, NotifyService notifyService)
	{
		if (deviseRepository == null)
		{
			throw new ArgumentNullException("deviseRepository");
		}
		_repository = deviseRepository;
		_notifyService = notifyService;
	}

	public int? Add(Devise devise)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateDevise);
		}
		List<Devise> list = GetAll().ToList();
		string code = devise.Code.ToUpper();
		if (list.Exists((Devise x) => x.Code.ToUpper().Equals(code)))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeviseCodeExist, code));
		}
		return _repository.Create(devise);
	}

	public void Delete(int deviseNo)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		Devise devise = Get(deviseNo);
		if (devise == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (!societeManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteDevise, devise.Code));
		}
		if (societeManager.GetAll().Any((Societe x) => x.HasDevise(deviseNo)))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeviseUtilisee, devise.Code));
		}
		_repository.Delete(devise);
	}

	public Devise Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		return _repository.Get(no);
	}

	public Devise Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException(code);
		}
		return _repository.Get(code);
	}

	public IEnumerable<Devise> GetAll()
	{
		return _repository.GetAll();
	}

	public decimal GetCoursPrevisionnel(DateTime dateCourante, DateTime dateEcheance, decimal coursCourant, decimal ibor, decimal tmm)
	{
		if (coursCourant <= 0m)
		{
			throw new ArgumentException("coursCourant");
		}
		if (tmm <= 0m)
		{
			throw new ArgumentException("tmm");
		}
		DateTime date = dateCourante.Date;
		DateTime date2 = dateEcheance.Date;
		if (date >= date2)
		{
			return coursCourant;
		}
		int num = GetIndiceSemaine(dateCourante, dateEcheance);
		if (num == 0)
		{
			return coursCourant;
		}
		if (num > 12)
		{
			num = 12;
		}
		return coursCourant * (1m + tmm / 100m * (decimal)num / 52m) / (1m + ibor / 100m * (decimal)num / 52m);
	}

	public int GetIndiceSemaine(DateTime dateCourante, DateTime dateEcheance)
	{
		DateTime date = dateCourante.Date;
		DateTime date2 = dateEcheance.Date;
		if (date >= date2)
		{
			return 0;
		}
		CultureInfo cultureInfo = CultureInfo.GetCultureInfo("Fr-fr");
		int weekOfYear = cultureInfo.Calendar.GetWeekOfYear(date2, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
		int weekOfYear2 = cultureInfo.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
		if (date.Year == date2.Year && weekOfYear2 == weekOfYear)
		{
			return 0;
		}
		return 52 * (date2.Year - date.Year) - weekOfYear2 + weekOfYear;
	}

	public Devise InitNew()
	{
		return new Devise();
	}

	public void Update(Devise devise)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		if (Groupe.DeviseManager.Get(devise.No) == null)
		{
			throw new ArgumentNullException("Devise invalide!");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		if (!societeManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateDevise, devise.Code));
		}
		_repository.Update(devise);
		Societe societe = societeManager.Societe;
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}
}
