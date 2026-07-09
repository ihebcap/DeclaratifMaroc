using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneDossierReglement
{
	public string BanqueTiers { get; set; }

	public decimal BaseRetenue { get; set; }

	public string CodeJournal { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateRecuperation { get; set; }

	public int DossierNo { get; set; }

	public DateTime EcheancePiece { get; set; }

	public bool IsBarre { get; set; }

	public bool IsComptabilise { get; set; }

	public bool IsLettre { get; set; }

	public bool IsPointe { get; set; }

	public bool IsRecuperer { get; set; }

	public string Lettrage { get; set; }

	public string Libelle { get; set; }

	public int? MaBanque { get; set; }

	public int? ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDevise { get; set; }

	public int No { get; set; }

	public int NombreTimbres { get; set; }

	public string Numero { get; set; }

	public string NumeroPieceComptable { get; set; }

	public string PieceReglement { get; set; }

	public string Pointage { get; set; }

	public string RecouvreurCin { get; set; }

	public string RecouvreurNom { get; set; }

	public string Reference { get; set; }

	public bool ReutiliserCheque { get; set; }

	public SensCompta? Sens { get; set; }

	public decimal Taux { get; set; }

	public int TiersNo { get; set; }

	public TiersType TiersType { get; set; }

	public TypeLigneDossier Type { get; set; }

	public string AffaireNumero { get; set; }
}
