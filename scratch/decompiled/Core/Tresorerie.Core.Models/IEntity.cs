namespace Tresorerie.Core.Models;

public interface IEntity
{
	int LockNo { get; set; }

	int No { get; }

	string Numero { get; }

	int SocieteNo { get; }

	TypeEntity TypeEntity { get; }
}
