using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneBordereau
{
	private readonly Func<ReglementClient> _reglementClientGetterDelegate;

	private Lazy<ReglementClient> _lazyReglementClient;

	private ReglementClient _reglementClient;

	public int BordereauNo { get; private set; }

	public string BordereauNumero { get; private set; }

	public NatureTypeBordereau BordereauNature { get; set; }

	public DateTime DateRemis { get; set; }

	public bool IsImpaye { get; private set; }

	public decimal Montant { get; private set; }

	public decimal MontantDeviseSociete { get; private set; }

	public string PieceNum { get; set; }

	public int No { get; private set; }

	public LigneBordereau(int no, int bordereauNo, string bordereauNumero, decimal montant, decimal montantDeviseSociete, bool isImpaye, Func<ReglementClient> reglementClientGetterDelegate)
	{
		_reglementClientGetterDelegate = reglementClientGetterDelegate ?? throw new ArgumentNullException("reglementClientGetterDelegate");
		No = no;
		BordereauNo = bordereauNo;
		IsImpaye = isImpaye;
		Montant = montant;
		MontantDeviseSociete = montantDeviseSociete;
		BordereauNumero = bordereauNumero;
	}

	public LigneBordereau(int no, decimal montant, Bordereau bordereau)
	{
		if (montant <= 0m)
		{
			throw new ArgumentNullException("montant");
		}
		if (bordereau == null)
		{
			throw new ArgumentNullException("bordereau");
		}
		No = no;
		BordereauNo = bordereau.No;
		Montant = montant;
		BordereauNumero = bordereau.Numero;
	}

	public LigneBordereau(ReglementClient reglement, Bordereau bordereau)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (bordereau == null)
		{
			throw new ArgumentNullException("bordereau");
		}
		No = reglement.No;
		BordereauNo = bordereau.No;
		Montant = reglement.Montant;
		MontantDeviseSociete = reglement.MontantDeviseSociete;
		IsImpaye = reglement.IsImpaye == ImpayeEtat.Impaye;
		BordereauNumero = bordereau.Numero;
	}

	public ReglementClient GetReglement()
	{
		if (_lazyReglementClient == null && _reglementClientGetterDelegate != null)
		{
			_lazyReglementClient = new Lazy<ReglementClient>(_reglementClientGetterDelegate);
		}
		if (_lazyReglementClient != null && !_lazyReglementClient.IsValueCreated)
		{
			_reglementClient = _lazyReglementClient.Value;
		}
		return _reglementClient;
	}
}
