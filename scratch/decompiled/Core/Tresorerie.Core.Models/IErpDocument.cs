using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpDocument
{
	string CodeAffaire { get; }

	int CollaborateurNo { get; }

	decimal Cours { get; }

	DateTime Date { get; }

	int DeviseNo { get; }

	string DocumentEntete1 { get; }

	string DocumentEntete2 { get; }

	string DocumentEntete3 { get; }

	string DocumentEntete4 { get; }

	ErpDomaine Domaine { get; }

	IEnumerable<IErpEcheance> Echeances { get; }

	int No { get; }

	string Numero { get; }

	short Provenance { get; }

	string Reference { get; }

	int SoucheNo { get; }

	string TiersNum { get; }

	string TiersPayeurNum { get; }

	decimal TotalDevise { get; }

	decimal TotalHorsTaxe { get; }

	decimal TotalTouteTaxe { get; }

	ErpDocumentType Type { get; }
}
