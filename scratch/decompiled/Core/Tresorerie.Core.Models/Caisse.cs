using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class Caisse
{
	private readonly Lazy<IEnumerable<CaisseModeReglement>> _lazyModesReglements;

	private readonly Lazy<IEnumerable<CaisseAuthorisation>> _lazyCaisseAuthorisation;

	private IEnumerable<CaisseModeReglement> _modeReglements;

	private IEnumerable<CaisseAuthorisation> _caisseAuthorisation;

	public string Code { get; private set; }

	public string CompteGeneral { get; set; }

	public int Depense { get; set; }

	public bool EnSommeil { get; set; }

	public string Intitule { get; set; }

	public bool IsDefault { get; set; }

	public string Journal { get; set; }

	public int No { get; private set; }

	public int Recette { get; set; }

	public int SocieteNo { get; set; }

	public Caisse(int societeNo, string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (societeNo <= 0)
		{
			throw new ArgumentException("Societe numero invalide!", "societeNo");
		}
		_modeReglements = new List<CaisseModeReglement>();
		_caisseAuthorisation = new List<CaisseAuthorisation>();
		SocieteNo = societeNo;
		Code = code;
		Intitule = string.Empty;
		Journal = string.Empty;
		CompteGeneral = string.Empty;
	}

	public Caisse(int no, string code, int societeNo, Lazy<IEnumerable<CaisseModeReglement>> lazyModeReglements, Lazy<IEnumerable<CaisseAuthorisation>> lazyCaisseAuthorisation)
		: this(societeNo, code)
	{
		if (lazyModeReglements == null)
		{
			throw new ArgumentNullException("lazyModeReglements");
		}
		if (lazyCaisseAuthorisation == null)
		{
			throw new ArgumentNullException("lazyCaisseAuthorisation");
		}
		No = no;
		Code = code;
		SocieteNo = societeNo;
		_lazyModesReglements = lazyModeReglements;
		_lazyCaisseAuthorisation = lazyCaisseAuthorisation;
	}

	public Caisse(string code, string intitule, int societeNo)
	{
		Code = code;
		Intitule = intitule;
		SocieteNo = societeNo;
		_modeReglements = new List<CaisseModeReglement>();
		_caisseAuthorisation = new List<CaisseAuthorisation>();
	}

	public bool CanAcceptReglement()
	{
		return !GetAllModes().Count().Equals(0);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is Caisse caisse))
		{
			return false;
		}
		return caisse.GetHashCode() == GetHashCode();
	}

	public IEnumerable<CaisseModeReglement> GetAllModes()
	{
		if (_lazyModesReglements != null && !_lazyModesReglements.IsValueCreated)
		{
			_modeReglements = _lazyModesReglements.Value.ToList();
		}
		return _modeReglements;
	}

	public IEnumerable<CaisseAuthorisation> GetAllCaisseAuthorisation()
	{
		if (_lazyCaisseAuthorisation != null && !_lazyCaisseAuthorisation.IsValueCreated)
		{
			_caisseAuthorisation = _lazyCaisseAuthorisation.Value.ToList();
		}
		return _caisseAuthorisation;
	}

	public override int GetHashCode()
	{
		return ((17 * 23 + No) * 23 + SocieteNo) * 23 + Code.GetHashCode();
	}

	public CaisseModeReglement GetMode(int modeNo)
	{
		return GetAllModes().ToList().SingleOrDefault((CaisseModeReglement x) => x.No == modeNo);
	}

	public bool HasModeReglement(int modeNo)
	{
		return GetAllModes().ToList().Exists((CaisseModeReglement x) => x.No == modeNo);
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoCaisse, No, Intitule);
	}
}
