using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DossierReglement : ICanBeComptabilise, ICanBeLettre
{
	public string Beneficiaire { get; set; }

	public int CaisseNo { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public int DeviseNo { get; set; }

	public string IdentifiantBeneficiaire { get; set; }

	public bool IsCloture { get; set; }

	public bool IsComptabilise { get; set; }

	public LettrageType Lettrage { get; set; }

	public string Lettre { get; set; } = string.Empty;

	public string Libelle { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantEcart { get; set; }

	public NatureFournisseur NatureBeneficiaire { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public int SocieteNo { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public int TiersNo { get; set; }

	public TiersType TiersType { get; set; }

	public int UtilisateurNo { get; set; }

	public StatutDossierReglement StatutDossier { get; set; }

	public bool IsDossierCommercial { get; set; }

	public int? AttestationRetenuNo { get; set; }

	public string AttestationRetenuNumero { get; set; }

	public PhaseDossierReglement PhaseDossier { get; set; }

	public DomaineDossier Domaine { get; set; }
}
