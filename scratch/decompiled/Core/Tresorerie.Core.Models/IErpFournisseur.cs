using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpFournisseur
{
	string Abrege { get; }

	string Adresse { get; }

	decimal AssuranceCredit { get; set; }

	int BanquePrincipalNo { get; }

	string CodePostal { get; }

	int CodeRisqueNo { get; }

	int CollaborateurNo { get; }

	string ComplementAdresse { get; }

	string CompteAnalytique { get; }

	string CompteGeneral { get; }

	short DeviseNo { get; }

	string Email { get; }

	decimal EncoursAutoriser { get; }

	string Identifiant { get; }

	string Intitule { get; }

	bool IsBloquer { get; }

	int No { get; }

	string Numero { get; }

	string PayeurNumero { get; }

	string Pays { get; }

	string Region { get; }

	string Site { get; }

	string Telecopie { get; }

	string Telephone { get; }

	TiersType Type { get; set; }

	string Ville { get; }

	int CategorieComptableNo { get; }

	bool EnSommeil { get; set; }

	string ChampStatistique1 { get; }

	string ChampStatistique2 { get; }

	string ChampStatistique3 { get; }

	string ChampStatistique4 { get; }

	string ChampStatistique5 { get; }

	string ChampStatistique6 { get; }

	string ChampStatistique7 { get; }

	string ChampStatistique8 { get; }

	string ChampStatistique9 { get; }

	string ChampStatistique10 { get; }
}
