using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class BordereauVirement : ICanBeComptabilise, ICanBeLettre
{
	private readonly Func<IEnumerable<LigneBordereauVirement>> _lignesGetterDelegate;

	private Lazy<IEnumerable<LigneBordereauVirement>> _lazyLignes;

	private IEnumerable<LigneBordereauVirement> _lignes;

	public int CaisseNo { get; private set; }

	public int CompteBanqueNo { get; private set; }

	public string CompteGeneral { get; set; }

	public DateTime Date { get; private set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public DateTime DateRemis { get; set; }

	public decimal DeviseCours { get; set; }

	public int DeviseNo { get; set; }

	public bool IsComptabilise { get; private set; }

	public bool IsPointe { get; set; }

	public bool IsRemis { get; set; }

	public string Journal { get; set; }

	public string Libelle { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; private set; }

	public string Numero { get; private set; }

	public string PieceNumero { get; set; }

	public int SocieteNo { get; private set; }

	public StatutTransfert StatutTransfert { get; set; }

	public int UserNo { get; set; }

	public string ExtraitNumero { get; set; }

	public bool IsFichierGenere { get; set; }

	public BordereauVirement(int no, string numero, int banqueNo, DateTime date, int caisseNo, int societeNo, bool isComptabiliser, Func<IEnumerable<LigneBordereauVirement>> lignesGetterDelegate)
		: this(numero, banqueNo, date, caisseNo, societeNo, isComptabiliser)
	{
		if (lignesGetterDelegate == null)
		{
			throw new ArgumentNullException("lignesGetterDelegate");
		}
		No = no;
		_lignesGetterDelegate = lignesGetterDelegate;
	}

	public BordereauVirement(string numero, int banqueNo, DateTime date, int caisseNo, int societeNo, bool isComptabiliser)
	{
		_lignes = new List<LigneBordereauVirement>();
		Numero = numero;
		CompteBanqueNo = banqueNo;
		Date = date;
		CaisseNo = caisseNo;
		SocieteNo = societeNo;
		IsComptabilise = isComptabiliser;
	}

	public void ChangeEtatComptabilise(bool etat)
	{
		IsComptabilise = etat;
	}

	public void ChangeNumero(string newNumero)
	{
		if (IsComptabilise)
		{
			throw new ApplicationException("Le bordereau est comptabilise.");
		}
		if (IsRemis)
		{
			throw new ApplicationException("Le bordereau est remis.");
		}
		Numero = newNumero;
	}

	public LigneBordereauVirement GetLigne(int no)
	{
		return GetLigneBordereaux().SingleOrDefault((LigneBordereauVirement x) => x.No == no);
	}

	public IEnumerable<LigneBordereauVirement> GetLigneBordereaux()
	{
		if (_lazyLignes == null && _lignesGetterDelegate != null)
		{
			_lazyLignes = new Lazy<IEnumerable<LigneBordereauVirement>>(_lignesGetterDelegate);
		}
		if (_lazyLignes != null && !_lazyLignes.IsValueCreated)
		{
			_lignes = _lazyLignes.Value;
		}
		return _lignes;
	}

	public void RefreshLignesBordereaux()
	{
		if (_lignesGetterDelegate != null)
		{
			_lazyLignes = null;
			_lazyLignes = new Lazy<IEnumerable<LigneBordereauVirement>>(_lignesGetterDelegate);
		}
	}
}
