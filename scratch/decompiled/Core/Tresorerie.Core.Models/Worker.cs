using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Worker
{
	public int No { get; set; }

	public int EntityNo { get; set; }

	public string EntityNumero { get; set; }

	public int SocieteNo { get; set; }

	public ErpDomaine Domaine { get; set; }

	public TypeEntity TypeEntity { get; set; }

	public ErpDocumentType? DocumentType { get; set; }

	public bool IsLocked { get; set; }

	public int UtilisateurCreateNo { get; set; }

	public int UtilisateurLockNo { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateLock { get; set; }
}
