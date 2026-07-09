using System;

namespace Tresorerie.Core.Enum;

public class EnumEx<T> where T : struct, IConvertible
{
	public string Description { get; set; }

	public T Value { get; set; }
}
