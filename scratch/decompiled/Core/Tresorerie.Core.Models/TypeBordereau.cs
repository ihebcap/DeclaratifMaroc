using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class TypeBordereau
{
	private readonly Func<IEnumerable<ModeReglement>> _modeReglementsGetterDelegate;

	private Lazy<IEnumerable<ModeReglement>> _lazyModeReglements;

	private IEnumerable<ModeReglement> _modes;

	public string Code { get; set; }

	public string Intitule { get; set; }

	public NatureTypeBordereau Nature { get; set; }

	public int No { get; private set; }

	public TypeBordereau()
	{
		_modes = new List<ModeReglement>();
		Code = string.Empty;
		Intitule = string.Empty;
	}

	public TypeBordereau(int no, string code, Func<IEnumerable<ModeReglement>> modeReglementsDelegate)
		: this()
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauTypeInvalide);
		}
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (modeReglementsDelegate == null)
		{
			throw new ArgumentNullException("modeReglementsDelegate");
		}
		No = no;
		Code = code;
		_modeReglementsGetterDelegate = modeReglementsDelegate;
	}

	public ModeReglement GetMode(int modeNo)
	{
		return GetModeReglements().ToList().SingleOrDefault((ModeReglement x) => x.No == modeNo);
	}

	public IEnumerable<ModeReglement> GetModeReglements()
	{
		if (_lazyModeReglements == null && _modeReglementsGetterDelegate != null)
		{
			_lazyModeReglements = new Lazy<IEnumerable<ModeReglement>>(_modeReglementsGetterDelegate);
		}
		if (_lazyModeReglements != null && !_lazyModeReglements.IsValueCreated)
		{
			_modes = _lazyModeReglements.Value.ToList();
		}
		return _modes;
	}

	public bool HasModeReglement(int modeNo)
	{
		return GetModeReglements().ToList().Exists((ModeReglement x) => x.No == modeNo);
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoTypeBordereauNumero, No, Intitule);
	}
}
