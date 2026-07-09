using System;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class TypeBordereauModeReglement
{
	private Lazy<ModeReglement> _modeReglementLazy;

	private Lazy<TypeBordereau> _typeBordereauLazy;

	public int bordereauTypeNo { get; set; }

	public ModeReglement ModeReglement => _modeReglementLazy?.Value;

	public int ModeReglementNo { get; set; }

	public TypeBordereau TypeBordereau => _typeBordereauLazy?.Value;

	public TypeBordereauModeReglement(Lazy<TypeBordereau> bordereauTypeLazy, Lazy<ModeReglement> modeReglementLazy)
	{
		if (bordereauTypeLazy == null)
		{
			throw new ArgumentNullException("bordereauTypeLazy");
		}
		if (modeReglementLazy == null)
		{
			throw new ArgumentNullException("modeReglementLazy");
		}
		_typeBordereauLazy = bordereauTypeLazy;
		_modeReglementLazy = modeReglementLazy;
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoTypeBordereau, bordereauTypeNo, ModeReglementNo);
	}
}
