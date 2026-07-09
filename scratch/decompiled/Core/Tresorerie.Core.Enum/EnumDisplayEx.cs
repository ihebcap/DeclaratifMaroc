using System;
using System.ComponentModel.DataAnnotations;

namespace Tresorerie.Core.Enum;

public class EnumDisplayEx<T> where T : struct, IConvertible
{
	[Display(Name = "ID")]
	public T Value { get; set; }

	[Display(Name = "Nom")]
	public string Name { get; set; }

	[Display(Name = "Abrev.")]
	public string ShortName { get; set; }

	[Display(Name = "Description")]
	public string Description { get; set; }
}
