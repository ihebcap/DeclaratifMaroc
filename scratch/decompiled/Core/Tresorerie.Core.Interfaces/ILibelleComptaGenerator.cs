using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Interfaces;

public interface ILibelleComptaGenerator
{
	string GetLibelle(string patternLibelle, DateTime date, string numero, string tiersNumero, string banque, string caisse, string mode, string piece, string banqueTiers, string libelle, DateTime? pieceEcheance, bool isEcritureDetaille, string beneficiaire, SensCompta? sens, NatureTypeBordereau? nature, string informationLibre1 = "", string informationLibre2 = "", string informationLibre3 = "", string informationLibre4 = "", string tiersIntitule = "", string reference = "", string pieceBordereau = "", string numeroReglementBord = "");
}
