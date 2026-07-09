using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpEncoursClient
{
	string ClientNumero { get; set; }

	decimal Cours { get; set; }

	DateTime Date { get; set; }

	string DesignationStatut { get; set; }

	int DeviseNo { get; set; }

	DocumentImporter IsImporter { get; }

	decimal MontantTtc { get; set; }

	int No { get; set; }

	int NombreEcheance { get; set; }

	int NombreEcheanceImportes { get; set; }

	string Numero { get; set; }

	ErpDocumentType Type { get; set; }
}
