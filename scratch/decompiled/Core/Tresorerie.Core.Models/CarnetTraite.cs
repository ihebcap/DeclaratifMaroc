using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class CarnetTraite
{
	private IEnumerable<Traite> _traiteCollection;

	private Lazy<IEnumerable<Traite>> _lazyTraites;

	public int BanqueNo { get; set; }

	public Func<IEnumerable<Traite>> TraitesGetterFunc { get; set; }

	public DateTime Date { get; set; }

	public string FirstTraite { get; set; }

	public bool IsEpuise => GetTraites().All((Traite x) => x.Statut != ChequeStatut.NonUtilise);

	public string LastTraite { get; set; }

	public int No { get; private set; }

	public int NombreTraite { get; set; }

	public int SocieteNo { get; set; }

	public CarnetTraite()
	{
		_traiteCollection = new List<Traite>();
	}

	public CarnetTraite(int no)
		: this()
	{
		No = no;
	}

	public Traite GetTraite(string numero)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		return GetTraites().SingleOrDefault((Traite x) => x.Numero == numero);
	}

	public Traite GetTraite(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		return GetTraites().SingleOrDefault((Traite x) => x.No == no);
	}

	public IEnumerable<Traite> GetTraites()
	{
		if (_lazyTraites == null && TraitesGetterFunc != null)
		{
			_lazyTraites = new Lazy<IEnumerable<Traite>>(TraitesGetterFunc);
		}
		if (_lazyTraites != null && !_lazyTraites.IsValueCreated)
		{
			_traiteCollection = _lazyTraites.Value.ToList();
		}
		return _traiteCollection;
	}
}
