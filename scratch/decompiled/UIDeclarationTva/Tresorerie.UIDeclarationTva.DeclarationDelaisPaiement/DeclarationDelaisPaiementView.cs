using System;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class DeclarationDelaisPaiementView : IEntity
{
	public int No { get; set; }

	public string Numero { get; set; }

	public int SocieteNo { get; set; }

	public TypeDeclarationDelaisPaiement TypeDeclaration { get; set; }

	public DateTime Date { get; set; }

	public int Exercice { get; set; }

	public DateTime DateDebut { get; set; }

	public DateTime DateFin { get; set; }

	public StatutDeclaration Statut { get; set; }

	public string Libelle { get; set; }

	public bool IsDepose { get; set; }

	public bool IsFichierGenerer { get; set; }

	public DeclarationTvaEncaissementTrimestrePeriode TrimestrePeriode { get; set; }

	public Guid Guid { get; private set; }

	public int LockNo { get; set; }

	public TypeEntity TypeEntity => TypeEntity.DeclarationDelaisPaiement;

	public DeclarationDelaisPaiementView()
	{
		Date = DateTime.Now;
		Guid = Guid.NewGuid();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is DeclarationDelaisPaiementView declarationDelaisPaiementView))
		{
			return false;
		}
		if (!(declarationDelaisPaiementView.Guid == Guid))
		{
			return declarationDelaisPaiementView.GetHashCode() == GetHashCode();
		}
		return true;
	}

	public override int GetHashCode()
	{
		return new { No, Numero }.GetHashCode();
	}
}
