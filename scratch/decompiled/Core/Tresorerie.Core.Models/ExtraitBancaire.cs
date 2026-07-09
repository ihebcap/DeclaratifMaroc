using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ExtraitBancaire
{
	public int No { get; set; }

	public string Reference { get; set; }

	public ExtraitBancaireEtat Etat { get; set; }

	public int InformationsBanqueNo { get; set; }

	public DateTime AncienneDate { get; set; }

	public DateTime NouvelleDate { get; set; }

	public decimal AncienSolde { get; set; }

	public decimal NouveauSolde { get; set; }

	public int SocieteNo { get; set; }

	public DateTime DateCreation { get; set; }

	public int UtilisateurNo { get; set; }

	public IList<LigneExtraitBancaire> Lignes { get; set; } = new List<LigneExtraitBancaire>();
}
