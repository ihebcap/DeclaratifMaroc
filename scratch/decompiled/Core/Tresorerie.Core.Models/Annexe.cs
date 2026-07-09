using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Annexe
{
	public AnnexeType AnnexeType { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public int No { get; private set; }

	public Annexe()
	{
		Code = string.Empty;
		Intitule = string.Empty;
	}

	public Annexe(int no)
		: this()
	{
		No = no;
	}

	public override bool Equals(object obj)
	{
		if (!(obj is Annexe annexe))
		{
			return false;
		}
		return annexe.GetHashCode() == obj.GetHashCode();
	}

	public override int GetHashCode()
	{
		return No ^ Code.GetHashCode();
	}

	public override string ToString()
	{
		return $"Annexe [{No}],[{Code}]";
	}
}
