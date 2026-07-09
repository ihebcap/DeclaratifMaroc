using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum Annexe5
{
	[Description("Montants payés égaux ou supérieurs à 1000 D y compris la TVA payés au titre des opérations d'export et des ventes des entreprises soumises à l'IS ou taux de 10 %")]
	A512,
	[Description("Montants égaux ou supérieurs à 1000D y compris la TVA payés au titre des autres opérations")]
	A514,
	[Description("Montants égaux ou supérieurs à 1000D y compris la TVA, payés par les entreprises et les établissements publics, et soumis à la retenue à la source au titre de la TVA.")]
	A516,
	[Description("Montants servis au titre des opérations réalisées avec les personnes n'ayant pas d'établissement en Tunisie et dont la retenue à la source au titre de la TVA est de 100 %")]
	A518
}
