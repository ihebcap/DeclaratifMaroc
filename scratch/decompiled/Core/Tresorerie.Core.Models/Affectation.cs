using System;

namespace Tresorerie.Core.Models;

public class Affectation
{
	private readonly Func<Echeance> _echeanceDelegate;

	private Echeance _echeance;

	private Lazy<Echeance> _lazyEcheance;

	public DateTime Date { get; private set; }

	public int? EcartEcheanceNo { get; private set; }

	public int EcheanceNo { get; private set; }

	public int ErpNo { get; private set; }

	public decimal Montant { get; internal set; }

	public decimal MontantDeviseSociete { get; private set; }

	public int No { get; private set; }

	public int ReglementNo { get; private set; }

	public int NombreJourReglement { get; set; }

	public int? DeclarationTvaEncaissementNo { get; set; }

	public int DelaisMoyenPayement { get; set; }

	public bool IsSynchro { get; set; }

	public bool IsImporterFromErp { get; set; }

	public Affectation(int erpNo, DateTime date, decimal montant, int reglementNo, int echeanceNo, decimal montantDevise, int? ecartEcheanceNo)
	{
		ErpNo = erpNo;
		Date = date;
		Montant = montant;
		ReglementNo = reglementNo;
		EcheanceNo = echeanceNo;
		MontantDeviseSociete = montantDevise;
		EcartEcheanceNo = ecartEcheanceNo;
	}

	public Affectation(int no, int erpNo, DateTime date, decimal montant, int reglementNo, int echeanceNo, decimal montantDevise, int? ecartEcheanceNo, Func<Echeance> clientEcheanceDelegate)
		: this(erpNo, date, montant, reglementNo, echeanceNo, montantDevise, ecartEcheanceNo)
	{
		_echeanceDelegate = clientEcheanceDelegate ?? throw new ArgumentNullException("clientEcheanceDelegate");
		No = no;
	}

	public Echeance GetEcheance()
	{
		if (_lazyEcheance == null && _echeanceDelegate != null)
		{
			_lazyEcheance = new Lazy<Echeance>(_echeanceDelegate);
		}
		if (_lazyEcheance != null && !_lazyEcheance.IsValueCreated)
		{
			_echeance = _lazyEcheance.Value;
		}
		return _echeance;
	}

	public override int GetHashCode()
	{
		return No.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj != null && obj is Affectation affectation)
		{
			return affectation.GetHashCode() == GetHashCode();
		}
		return false;
	}
}
