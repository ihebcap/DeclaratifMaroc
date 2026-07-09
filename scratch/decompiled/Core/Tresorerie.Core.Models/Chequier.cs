using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Chequier
{
	private IEnumerable<Cheque> _chequeCollection;

	private Lazy<IEnumerable<Cheque>> _lazyCheques;

	public int BanqueNo { get; set; }

	public Func<IEnumerable<Cheque>> ChequesGetterFunc { get; set; }

	public DateTime Date { get; set; }

	public string FirstCheque { get; set; }

	public bool IsEpuise => GetCheques().All((Cheque x) => x.Statut != ChequeStatut.NonUtilise);

	public string LastCheque { get; set; }

	public int No { get; private set; }

	public int NombreCheque { get; set; }

	public int SocieteNo { get; set; }

	public DateTime? DateValiditer { get; set; }

	public decimal? MontantPlafond { get; set; }

	public Chequier()
	{
		_chequeCollection = new List<Cheque>();
	}

	public Chequier(int no)
		: this()
	{
		No = no;
	}

	public Cheque GetCheque(string numero)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		return GetCheques().SingleOrDefault((Cheque x) => x.Numero == numero);
	}

	public Cheque GetCheque(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		return GetCheques().SingleOrDefault((Cheque x) => x.No == no);
	}

	public IEnumerable<Cheque> GetCheques()
	{
		if (_lazyCheques == null && ChequesGetterFunc != null)
		{
			_lazyCheques = new Lazy<IEnumerable<Cheque>>(ChequesGetterFunc);
		}
		if (_lazyCheques != null && !_lazyCheques.IsValueCreated)
		{
			_chequeCollection = _lazyCheques.Value.ToList();
		}
		return _chequeCollection;
	}
}
