using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ParametreMailing
{
	public string EnteteMail { get; set; }

	public string PiedMail { get; set; }

	public string smtp { get; set; }

	public string Port { get; set; }

	public string accountName { get; set; }

	public string accountPass { get; set; }

	public string Subject { get; set; }

	public int SocieteNo { get; set; }

	public string Cc { get; set; }

	public string Cci { get; set; }

	public bool SSL { get; set; }

	public TypeMailling TypeMailling { get; set; }

	public string EnteteMailRecouvrementRepresentant { get; set; }

	public string PiedMailRecouvrementRepresentant { get; set; }

	public string SmtpRecouvrementRepresentant { get; set; }

	public string PortRecouvrementRepresentant { get; set; }

	public string AccountNameRecouvrementRepresentant { get; set; }

	public string AccountPassRecouvrementRepresentant { get; set; }

	public string SubjectRecouvrementRepresentant { get; set; }

	public string CcRecouvrementRepresentant { get; set; }

	public string CciRecouvrementRepresentant { get; set; }

	public bool SSLRecouvrementRepresentant { get; set; }

	public TypeMailling TypeMaillingRecouvrementRepresentant { get; set; }
}
