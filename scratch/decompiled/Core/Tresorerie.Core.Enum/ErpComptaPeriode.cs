using System.ComponentModel.DataAnnotations;

namespace Tresorerie.Core.Enum;

public enum ErpComptaPeriode
{
	[Display(Description = "Janvier", ShortName = "janv.")]
	Janvier = 1,
	[Display(Description = "Février", ShortName = "févr.")]
	Février,
	[Display(Description = "Mars", ShortName = "mars")]
	Mars,
	[Display(Description = "Avril", ShortName = "avr.")]
	Avril,
	[Display(Description = "Mai", ShortName = "mai")]
	Mai,
	[Display(Description = "Juin", ShortName = "juin")]
	Juin,
	[Display(Description = "Juillet", ShortName = "juil.")]
	Juillet,
	[Display(Description = "Août", ShortName = "août")]
	Août,
	[Display(Description = "Septembre", ShortName = "sept.")]
	Septembre,
	[Display(Description = "Octobre", ShortName = "oct.")]
	Octobre,
	[Display(Description = "Novembre", ShortName = "nov.")]
	Novembre,
	[Display(Description = "Décembre", ShortName = "déc.")]
	Décembre
}
