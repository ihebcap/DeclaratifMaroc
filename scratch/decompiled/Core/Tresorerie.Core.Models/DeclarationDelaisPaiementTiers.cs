using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DeclarationDelaisPaiementTiers
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public string Numero { get; set; }

	public TypeDeclarationDelaisPaiement TypeDeclaration { get; set; }

	public DateTime Date { get; set; }

	public int Exercice { get; set; }

	public DateTime DateDebut { get; set; }

	public DateTime DateFin { get; set; }

	public StatutDeclaration Statut { get; set; }

	public int CreateurNo { get; set; }

	public DateTime DateCreation { get; set; }

	public int ModificateurNo { get; set; }

	public DateTime DateModification { get; set; }

	public bool IsDepose { get; set; }

	public string Libelle { get; set; }

	public bool IsFichierGenerer { get; set; }

	public DeclarationTvaEncaissementTrimestrePeriode TrimestrePeriode { get; set; }
}
