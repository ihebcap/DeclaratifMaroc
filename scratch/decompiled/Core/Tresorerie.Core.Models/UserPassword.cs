using System;

namespace Tresorerie.Core.Models;

public class UserPassword
{
	public int Id { get; set; }

	public int UserId { get; set; }

	public byte[] Hash { get; set; }

	public byte[] Salt { get; set; }

	public DateTime DateModification { get; set; }

	public override bool Equals(object obj)
	{
		if (!(obj is UserPassword))
		{
			return false;
		}
		return (obj as UserPassword).GetHashCode() == GetHashCode();
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}
}
