using System;

namespace Tresorerie.Core.Models;

public class LigneBordereauVirement
{
	private readonly Func<ReglementFournisseur> _reglementFournisseurGetterDelegate;

	private Lazy<ReglementFournisseur> _lazyReglementFournisseur;

	private ReglementFournisseur _reglementFournisseur;

	public int BordereauNo { get; private set; }

	public string BordereauNumero { get; private set; }

	public DateTime DateRemis { get; set; }

	public decimal Montant { get; private set; }

	public decimal MontantDeviseSociete { get; private set; }

	public int No { get; private set; }

	public LigneBordereauVirement(int no, int bordereauNo, string bordereauNumero, decimal montant, decimal montantDeviseSociete, Func<ReglementFournisseur> reglementFournisseurGetterDelegate)
	{
		_reglementFournisseurGetterDelegate = reglementFournisseurGetterDelegate ?? throw new ArgumentNullException("reglementFournisseurGetterDelegate");
		No = no;
		BordereauNo = bordereauNo;
		Montant = montant;
		MontantDeviseSociete = montantDeviseSociete;
		BordereauNumero = bordereauNumero;
	}

	public LigneBordereauVirement(int no, decimal montant, BordereauVirement bordereau)
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

	public LigneBordereauVirement(ReglementFournisseur reglement, BordereauVirement bordereau)
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
		BordereauNumero = bordereau.Numero;
	}

	public ReglementFournisseur GetReglement()
	{
		if (_lazyReglementFournisseur == null && _reglementFournisseurGetterDelegate != null)
		{
			_lazyReglementFournisseur = new Lazy<ReglementFournisseur>(_reglementFournisseurGetterDelegate);
		}
		if (_lazyReglementFournisseur != null && !_lazyReglementFournisseur.IsValueCreated)
		{
			_reglementFournisseur = _lazyReglementFournisseur.Value;
		}
		return _reglementFournisseur;
	}
}
