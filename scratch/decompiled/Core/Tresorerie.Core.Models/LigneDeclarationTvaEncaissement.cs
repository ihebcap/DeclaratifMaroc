using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneDeclarationTvaEncaissement
{
	public int No { get; set; }

	public int DeclarationNo { get; set; }

	public int EntityNo { get; set; }

	public LigneDeclarationTvaEncaissementEntityType EntityType { get; set; }

	public bool IsReport { get; set; }

	public string MouvementNumero { get; set; }

	public string DocumentNumero { get; set; }

	public string DesignationDocument { get; set; }

	public decimal Assiette { get; set; }

	public decimal Taux { get; set; }

	public decimal Montant { get; set; }

	public LigneDeclarationTvaEncaissementDomaine Domaine { get; set; }

	public LigneDeclarationTvaEncaissementIntegrationType TypeIntegration { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersIdentifiant { get; set; }

	public ReglementType TypePayement { get; set; }

	public DateTime DateMouvement { get; set; }

	public DateTime DateDocument { get; set; }

	public string TiersIce { get; set; }

	public string ErpTaxeCode { get; set; }

	public string CodeActivite { get; set; }

	public decimal Prorata { get; set; }
}
