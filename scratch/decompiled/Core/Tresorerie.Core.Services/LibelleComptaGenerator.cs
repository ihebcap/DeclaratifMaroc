using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class LibelleComptaGenerator : ILibelleComptaGenerator
{
	private readonly IList<ParameterLibelleCompta> ListParam;

	private readonly ILibelleComptaFormuleRunner _libelleFormuleRunner;

	private const string PatternFunctionBordereau = "[[A-Za-z]+]";

	private const string PatternSens = "%S";

	private const string PatternBeneficiaire = "%G";

	private const string PatternEcritureDetaillee = "%K";

	private const string PatternPieceEcheance = "%J";

	private const string PatternInfoLibre1 = "%F1";

	private const string PatternInfoLibre2 = "%F2";

	private const string PatternInfoLibre3 = "%F3";

	private const string PatternInfoLibre4 = "%F4";

	private const string PatternTiers = "%T";

	private const string PatternTiersIntitule = "%I";

	private const string PatternBanque = "%B";

	private const string PatternBanqueTiers = "%E";

	private const string PatternCaisse = "%C";

	private const string PatternDate = "%D";

	private const string PatternLibelle = "%L";

	private const string PatternMode = "%M";

	private const string PatternNature = "%A";

	private const string PatternNumero = "%N";

	private const string PatternPiece = "%P";

	private const string PatternPieceBordereau = "%H";

	private const string PatternReference = "%R";

	private const string PatternNumReglementBord = "%Q";

	public LibelleComptaGenerator(ILibelleComptaFormuleRunner libelleFormuleRunner)
		: this()
	{
		_libelleFormuleRunner = libelleFormuleRunner ?? throw new ArgumentNullException("libelleFormuleRunner");
	}

	private LibelleComptaGenerator()
	{
		ListParam = new List<ParameterLibelleCompta>
		{
			new ParameterLibelleCompta
			{
				Pattern = "%N",
				Type = TypePropriety.Numero
			},
			new ParameterLibelleCompta
			{
				Pattern = "%T",
				Type = TypePropriety.Tiers
			},
			new ParameterLibelleCompta
			{
				Pattern = "%D",
				Type = TypePropriety.Date
			},
			new ParameterLibelleCompta
			{
				Pattern = "%C",
				Type = TypePropriety.Caisse
			},
			new ParameterLibelleCompta
			{
				Pattern = "%B",
				Type = TypePropriety.Banque
			},
			new ParameterLibelleCompta
			{
				Pattern = "%M",
				Type = TypePropriety.Mode
			},
			new ParameterLibelleCompta
			{
				Pattern = "%P",
				Type = TypePropriety.Piece
			},
			new ParameterLibelleCompta
			{
				Pattern = "%H",
				Type = TypePropriety.PieceBordereau
			},
			new ParameterLibelleCompta
			{
				Pattern = "%I",
				Type = TypePropriety.TiersIntitule
			},
			new ParameterLibelleCompta
			{
				Pattern = "%L",
				Type = TypePropriety.Libelle
			},
			new ParameterLibelleCompta
			{
				Pattern = "%A",
				Type = TypePropriety.Nature
			},
			new ParameterLibelleCompta
			{
				Pattern = "%E",
				Type = TypePropriety.BanqueTiers
			},
			new ParameterLibelleCompta
			{
				Pattern = "%R",
				Type = TypePropriety.Reference
			},
			new ParameterLibelleCompta
			{
				Pattern = "%F1",
				Type = TypePropriety.InfoLibre1
			},
			new ParameterLibelleCompta
			{
				Pattern = "%F2",
				Type = TypePropriety.InfoLibre2
			},
			new ParameterLibelleCompta
			{
				Pattern = "%F3",
				Type = TypePropriety.InfoLibre3
			},
			new ParameterLibelleCompta
			{
				Pattern = "%F4",
				Type = TypePropriety.InfoLibre4
			},
			new ParameterLibelleCompta
			{
				Pattern = "%J",
				Type = TypePropriety.PieceEcheance
			},
			new ParameterLibelleCompta
			{
				Pattern = "[[A-Za-z]+]",
				Type = TypePropriety.Formula
			},
			new ParameterLibelleCompta
			{
				Pattern = "%S",
				Type = TypePropriety.Sens
			},
			new ParameterLibelleCompta
			{
				Pattern = "%K",
				Type = TypePropriety.EcritureDetaillee
			},
			new ParameterLibelleCompta
			{
				Pattern = "%G",
				Type = TypePropriety.Beneficiaire
			},
			new ParameterLibelleCompta
			{
				Pattern = "%Q",
				Type = TypePropriety.NumeroReglementBord
			}
		};
	}

	public string GetLibelle(string patternLibelle, DateTime date, string numero, string tiersNumero, string banque, string caisse, string mode, string piece, string banqueTiers, string libelle, DateTime? pieceEcheance, bool isEcritureDetaille, string beneficiaire, SensCompta? sens, NatureTypeBordereau? nature, string informationLibre1 = "", string informationLibre2 = "", string informationLibre3 = "", string informationLibre4 = "", string tiersIntitule = "", string reference = "", string pieceBordereau = "", string numeroReglementBord = "")
	{
		List<ParameterLibelleCompta> list = new List<ParameterLibelleCompta>();
		foreach (ParameterLibelleCompta item in FilterParameters(ListParam, patternLibelle).ToList())
		{
			Regex regex = new Regex(item.Pattern);
			switch (item.Type)
			{
			case TypePropriety.Formula:
			{
				string value = regex.Match(patternLibelle).Value.Replace("[", "").Replace("]", "");
				patternLibelle = regex.Replace(patternLibelle, string.Empty);
				item.Value = value;
				list.Add(item);
				break;
			}
			case TypePropriety.PieceEcheance:
				patternLibelle = regex.Replace(patternLibelle, pieceEcheance.GetValueOrDefault().ToString("dd/MM/yyyy"));
				item.Value = pieceEcheance.GetValueOrDefault();
				list.Add(item);
				break;
			case TypePropriety.Sens:
				item.Value = sens.GetValueOrDefault();
				list.Add(item);
				break;
			case TypePropriety.EcritureDetaillee:
				item.Value = isEcritureDetaille;
				list.Add(item);
				break;
			case TypePropriety.Date:
				patternLibelle = regex.Replace(patternLibelle, date.ToString("dd/MM/yyyy"));
				item.Value = date;
				list.Add(item);
				break;
			case TypePropriety.Numero:
				patternLibelle = regex.Replace(patternLibelle, numero ?? string.Empty);
				item.Value = numero;
				list.Add(item);
				break;
			case TypePropriety.Beneficiaire:
				patternLibelle = regex.Replace(patternLibelle, beneficiaire ?? string.Empty);
				item.Value = beneficiaire;
				list.Add(item);
				break;
			case TypePropriety.Tiers:
				patternLibelle = regex.Replace(patternLibelle, tiersNumero ?? string.Empty);
				item.Value = tiersNumero;
				list.Add(item);
				break;
			case TypePropriety.Banque:
				patternLibelle = regex.Replace(patternLibelle, banque ?? string.Empty);
				item.Value = banque;
				list.Add(item);
				break;
			case TypePropriety.Caisse:
				patternLibelle = regex.Replace(patternLibelle, caisse ?? string.Empty);
				item.Value = caisse;
				list.Add(item);
				break;
			case TypePropriety.Mode:
				patternLibelle = regex.Replace(patternLibelle, mode ?? string.Empty);
				item.Value = mode;
				list.Add(item);
				break;
			case TypePropriety.Piece:
				patternLibelle = regex.Replace(patternLibelle, piece ?? string.Empty);
				item.Value = piece;
				list.Add(item);
				break;
			case TypePropriety.PieceBordereau:
				patternLibelle = regex.Replace(patternLibelle, pieceBordereau ?? string.Empty);
				item.Value = pieceBordereau;
				list.Add(item);
				break;
			case TypePropriety.TiersIntitule:
				patternLibelle = regex.Replace(patternLibelle, tiersIntitule ?? string.Empty);
				item.Value = tiersIntitule;
				list.Add(item);
				break;
			case TypePropriety.Libelle:
				patternLibelle = regex.Replace(patternLibelle, libelle ?? string.Empty);
				item.Value = libelle;
				list.Add(item);
				break;
			case TypePropriety.Nature:
			{
				string text = string.Empty;
				if (nature.HasValue)
				{
					switch (nature.Value)
					{
					case NatureTypeBordereau.Encaissement:
						text = "ENC";
						break;
					case NatureTypeBordereau.Escompte:
						text = "ESC";
						break;
					case NatureTypeBordereau.Factoring:
						text = "FAC";
						break;
					}
				}
				patternLibelle = regex.Replace(patternLibelle, text ?? string.Empty);
				item.Value = text;
				list.Add(item);
				break;
			}
			case TypePropriety.BanqueTiers:
				patternLibelle = regex.Replace(patternLibelle, banqueTiers ?? string.Empty);
				item.Value = banqueTiers;
				list.Add(item);
				break;
			case TypePropriety.Reference:
				patternLibelle = regex.Replace(patternLibelle, reference ?? string.Empty);
				item.Value = reference;
				list.Add(item);
				break;
			case TypePropriety.InfoLibre1:
				patternLibelle = regex.Replace(patternLibelle, informationLibre1 ?? string.Empty);
				item.Value = informationLibre1;
				list.Add(item);
				break;
			case TypePropriety.InfoLibre2:
				patternLibelle = regex.Replace(patternLibelle, informationLibre2 ?? string.Empty);
				item.Value = informationLibre2;
				list.Add(item);
				break;
			case TypePropriety.InfoLibre3:
				patternLibelle = regex.Replace(patternLibelle, informationLibre3 ?? string.Empty);
				item.Value = informationLibre3;
				list.Add(item);
				break;
			case TypePropriety.InfoLibre4:
				patternLibelle = regex.Replace(patternLibelle, informationLibre4 ?? string.Empty);
				item.Value = informationLibre4;
				list.Add(item);
				break;
			case TypePropriety.NumeroReglementBord:
				patternLibelle = regex.Replace(patternLibelle, numeroReglementBord ?? string.Empty);
				item.Value = numeroReglementBord;
				list.Add(item);
				break;
			}
		}
		if (list.Any((ParameterLibelleCompta x) => x.Type == TypePropriety.Formula))
		{
			patternLibelle = _libelleFormuleRunner.Run(list);
		}
		return patternLibelle;
	}

	private IEnumerable<ParameterLibelleCompta> FilterParameters(IEnumerable<ParameterLibelleCompta> parameters, string sql)
	{
		if (string.IsNullOrEmpty(sql))
		{
			throw new ArgumentNullException("sql");
		}
		sql = sql.Replace("]", "]#").Replace("%", "#%");
		List<ParameterLibelleCompta> list = new List<ParameterLibelleCompta>();
		string[] array = sql.Split('#');
		foreach (string item in array)
		{
			IEnumerable<ParameterLibelleCompta> collection = parameters.Where((ParameterLibelleCompta p) => Regex.IsMatch(item, p.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant));
			list.AddRange(collection);
		}
		return list;
	}
}
