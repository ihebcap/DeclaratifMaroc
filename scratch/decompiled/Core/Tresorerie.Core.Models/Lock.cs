using System;

namespace Tresorerie.Core.Models;

public class Lock
{
	public DateTime Date { get; set; }

	public TypeEntity Entity { get; set; }

	public int EntityId { get; set; }

	public int Id { get; set; }

	public byte[] Rowversion { get; set; }

	public int SocieteNo { get; set; }

	public Guid SubscriberId { get; set; }
}
