using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Previsionnelle
{
	private readonly List<TypePrevisionnel> _listSolde;

	private TypePrevisionnel _typePrevisionnel = TypePrevisionnel.Prevision;

	public int? BanqueErpNo { get; set; }

	public int BanquePrevueNoErp { get; set; }

	public int BanquePrevueNo { get; set; }

	public string CodeRegroupementBanque { get; set; }

	public string BordereauNumero { get; set; }

	public int CaisseNo { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal Cours { get; set; }

	public decimal CoursPrevisionnel { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateImpaye { get; set; }

	public DateTime DatePointe { get; set; }

	public DateTime DatePrevu
	{
		get
		{
			if (!IsImpaye || !IsRemisBanque)
			{
				if (!IsPointe)
				{
					if (!IsRemisBanque)
					{
						return EcheancePrevue;
					}
					return RemisDate;
				}
				return DatePointe.Date;
			}
			return DateImpaye.Date;
		}
	}

	public int DeviseNo { get; set; }

	public DomainePrevisionnelle Domaine { get; set; }

	public string DossierNumero { get; set; }

	public DateTime Echeance { get; set; }

	public DateTime EcheancePrevue { get; set; }

	public bool IsAnnule { get; set; }

	public int IsEscompteRegler { get; set; }

	public bool IsImpaye { get; set; }

	public bool IsPointe { get; set; }

	public Remis IsRemis { get; set; }

	public bool IsRemisBanque => IsRemis == Remis.RemisBanque;

	public bool IsRemplace { get; set; }

	public string Libelle { get; set; }

	public int SocieteNo { get; set; }

	public int ModeNo { get; set; }

	public decimal MontantDevise { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int MouvementNo { get; set; }

	public NatureTypeBordereau NatureBordereau { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public string PieceBanque { get; set; }

	public DateTime RemisDate { get; set; }

	public string Rib { get; set; }

	public SensPrevisionnelle Sens { get; set; }

	public decimal SoldeDevise { get; set; }

	public decimal SoldeDeviseSociete { get; set; }

	public int? TiersNo { get; set; }

	public string Tire { get; set; }

	public ReglementType Type { get; set; }

	public bool IsComptaOperationBancaire { get; set; }

	public DateTime DateComptaOperationBancaire { get; set; }

	public string AffaireNumero { get; set; }

	public TypePrevisionnel TypePrevisionnel
	{
		get
		{
			if (_listSolde.Any((TypePrevisionnel p) => p == _typePrevisionnel))
			{
				return _typePrevisionnel;
			}
			if (IsPointe)
			{
				return TypePrevisionnel.Realise;
			}
			if (IsRemisBanque || Type == ReglementType.Virement)
			{
				return TypePrevisionnel.EnCours;
			}
			return TypePrevisionnel.Prevision;
		}
		set
		{
			_typePrevisionnel = value;
		}
	}

	public Previsionnelle()
	{
		_listSolde = new List<TypePrevisionnel>
		{
			TypePrevisionnel.SoldeBancaire,
			TypePrevisionnel.SoldeInitial
		};
	}

	public override string ToString()
	{
		return $"{EcheancePrevue:yyMMdd} {Numero} {MontantDevise}";
	}
}
