using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class VirementTiers : ICanBeComptabilise
{
	private readonly Func<IEnumerable<LigneVirementTiers>> _lignesGetterDelegate;

	private Lazy<IEnumerable<LigneVirementTiers>> _lazyLignes;

	private IEnumerable<LigneVirementTiers> _lignes;

	public int CaisseNo { get; set; }

	public int CompteNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DatePointage { get; set; }

	public decimal Cours { get; set; }

	public int DeviseNo { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public bool IsPointer { get; set; }

	public string Libelle { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string PieceNumero { get; set; }

	public int SocieteNo { get; set; }

	public TiersType TypeTiers { get; set; }

	public int UtilisateurNo { get; set; }

	public VirementTiers(Func<IEnumerable<LigneVirementTiers>> lignesGetterDelegate)
	{
		_lignesGetterDelegate = lignesGetterDelegate ?? throw new ArgumentNullException("lignesGetterDelegate");
	}

	public VirementTiers()
	{
	}

	public void ChangeEtatComptabilise(EtatComptabilite etatComptabilite)
	{
		IsComptabilise = etatComptabilite;
	}

	public LigneVirementTiers GetLigne(int no)
	{
		return GetLignes().SingleOrDefault((LigneVirementTiers x) => x.No == no);
	}

	public IEnumerable<LigneVirementTiers> GetLignes()
	{
		if (_lazyLignes == null && _lignesGetterDelegate != null)
		{
			_lazyLignes = new Lazy<IEnumerable<LigneVirementTiers>>(_lignesGetterDelegate);
		}
		if (_lazyLignes != null && !_lazyLignes.IsValueCreated)
		{
			_lignes = _lazyLignes.Value;
		}
		return _lignes;
	}
}
