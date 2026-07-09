using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Bordereau : ICanBeComptabilise, ICanBeLettre
{
	private readonly Func<IEnumerable<LigneBordereau>> _lignesGetterDelegate;

	private Lazy<IEnumerable<LigneBordereau>> _lazyLignes;

	private IEnumerable<LigneBordereau> _lignes;

	public NatureTypeBordereau BordereauNature { get; set; }

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

	public bool IsImpaye { get; set; }

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

	public int TypeNo { get; private set; }

	public int UserNo { get; set; }

	public string ExtraitNumero { get; set; }

	public decimal? MontantFrais { get; set; }

	public decimal? MontantTvaFrais { get; set; }

	public string InfoLibre1 { get; set; }

	public string InfoLibre2 { get; set; }

	public string InfoLibre3 { get; set; }

	public string InfoLibre4 { get; set; }

	public Bordereau(int no, string numero, int banqueNo, DateTime date, int caisseNo, int societeNo, int typeNo, bool isComptabiliser, Func<IEnumerable<LigneBordereau>> lignesGetterDelegate)
		: this(numero, banqueNo, date, caisseNo, societeNo, typeNo, isComptabiliser)
	{
		if (lignesGetterDelegate == null)
		{
			throw new ArgumentNullException("lignesGetterDelegate");
		}
		No = no;
		_lignesGetterDelegate = lignesGetterDelegate;
	}

	public Bordereau(string numero, int banqueNo, DateTime date, int caisseNo, int societeNo, int typeNo, bool isComptabiliser)
	{
		_lignes = new List<LigneBordereau>();
		Numero = numero;
		CompteBanqueNo = banqueNo;
		Date = date;
		CaisseNo = caisseNo;
		SocieteNo = societeNo;
		TypeNo = typeNo;
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

	public LigneBordereau GetLigne(int no)
	{
		return GetLigneBordereaux().SingleOrDefault((LigneBordereau x) => x.No == no);
	}

	public IEnumerable<LigneBordereau> GetLigneBordereaux()
	{
		if (_lazyLignes == null && _lignesGetterDelegate != null)
		{
			_lazyLignes = new Lazy<IEnumerable<LigneBordereau>>(_lignesGetterDelegate);
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
			_lazyLignes = new Lazy<IEnumerable<LigneBordereau>>(_lignesGetterDelegate);
		}
	}
}
