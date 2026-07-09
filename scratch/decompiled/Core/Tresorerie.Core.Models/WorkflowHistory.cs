using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class WorkflowHistory
{
	public int No { get; set; }

	public TypeEntity EntityType { get; set; }

	public int EntityNo { get; set; }

	public WorkflowValidationStatut Statut { get; set; }

	public string Commentaire { get; set; }

	public int UserNo { get; set; }

	public int SocieteNo { get; set; }

	public DateTime DateCreation { get; set; }
}
