using System;

namespace Tresorerie.Core.Models;

public class Remplacement : ICanBeComptabilise
{
	private readonly Func<ReglementClient> _reglementRemplacantGetterDelegate;

	private readonly Func<ReglementClient> _reglementRemplaceGetterDelegate;

	private Lazy<ReglementClient> _lazyReglementRemplacant;

	private Lazy<ReglementClient> _lazyReglementRemplace;

	private ReglementClient _reglementRemplacant;

	private ReglementClient _reglementRemplace;

	public decimal Montant { get; set; }

	public int No { get; private set; }

	public int ReglementRemplacantNo { get; private set; }

	public int ReglementRemplaceNo { get; private set; }

	public int SocieteNo { get; private set; }

	public Remplacement(int reglementRemplacantNo, decimal montant, int reglementRemplaceNo, int societeNo)
	{
		if (reglementRemplacantNo <= 0)
		{
			throw new ArgumentNullException("reglementRemplacantNo");
		}
		if (reglementRemplaceNo <= 0)
		{
			throw new ArgumentNullException("reglementRemplaceNo");
		}
		if (societeNo <= 0)
		{
			throw new ArgumentNullException("societeNo");
		}
		ReglementRemplacantNo = reglementRemplacantNo;
		Montant = montant;
		ReglementRemplaceNo = reglementRemplaceNo;
		SocieteNo = societeNo;
	}

	public Remplacement(int no, int reglementRemplacementNo, decimal montant, int reglementRemplaceNo, int societeNo, Func<ReglementClient> reglementRemplaceGetterDelegate, Func<ReglementClient> reglementRemplacantGetterDelegate)
		: this(reglementRemplacementNo, montant, reglementRemplaceNo, societeNo)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		_reglementRemplaceGetterDelegate = reglementRemplaceGetterDelegate ?? throw new ArgumentNullException("reglementRemplaceGetterDelegate");
		_reglementRemplacantGetterDelegate = reglementRemplacantGetterDelegate ?? throw new ArgumentNullException("reglementRemplacantGetterDelegate");
		No = no;
	}

	public ReglementClient GetReglementRemplacant()
	{
		if (_lazyReglementRemplacant == null && _reglementRemplacantGetterDelegate != null)
		{
			_lazyReglementRemplacant = new Lazy<ReglementClient>(_reglementRemplacantGetterDelegate);
		}
		if (_lazyReglementRemplacant != null && !_lazyReglementRemplacant.IsValueCreated)
		{
			_reglementRemplacant = _lazyReglementRemplacant.Value;
		}
		return _reglementRemplacant;
	}

	public ReglementClient GetReglementToRemplace()
	{
		if (_lazyReglementRemplace == null && _reglementRemplaceGetterDelegate != null)
		{
			_lazyReglementRemplace = new Lazy<ReglementClient>(_reglementRemplaceGetterDelegate);
		}
		if (_lazyReglementRemplace != null && !_lazyReglementRemplace.IsValueCreated)
		{
			_reglementRemplace = _lazyReglementRemplace.Value;
		}
		return _reglementRemplace;
	}

	public override int GetHashCode()
	{
		return No.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj != null && obj is Remplacement remplacement)
		{
			return remplacement.GetHashCode() == GetHashCode();
		}
		return false;
	}
}
