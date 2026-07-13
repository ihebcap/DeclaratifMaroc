using System;

namespace Declaration.Selection
{
    /// <summary>
    /// SOURCE UNIQUE de la règle « dans quelle période tombe un règlement » (TASK-062).
    ///
    /// Deux briques réutilisées à l'IDENTIQUE par les 3 chemins (interrogation Rapprochement,
    /// sélection règlement-first, déclaration facture-first) — comme le pattern
    /// <c>ReglementRapprochementRow.EstRapprocheBanque</c> (expression unique répliquée SQL ⇄ C#) :
    ///
    ///   • <see cref="DateReferencePour"/> — la date qui situe un règlement dans une période :
    ///       espèce (MV_Type=0)                → MV_Date
    ///       non-espèce rapproché (MV_Point=1) → MV_PointDate
    ///       non-espèce non rapproché          → MV_Date
    ///
    ///   • <see cref="EstDeclarable"/> — gate propre à la déclaration : espèce (auto-rapprochée)
    ///     OU rapproché banque.
    ///
    /// La dérivation C# (<see cref="DateReferencePour"/>) et le fragment SQL
    /// (<see cref="DateReferenceSqlM"/>) DOIVENT rester répliqués à l'identique
    /// (test anti-divergence). Toute modification de l'un impose l'autre.
    /// </summary>
    public static class RegleDatePeriode
    {
        /// <summary>
        /// Date de référence de période. Renvoie <c>null</c> si un non-espèce rapproché
        /// (MV_Point=1) n'a pas de MV_PointDate — donnée incohérente signalée au VERIFY.
        /// Reproduit EXACTEMENT le <c>CASE</c> SQL (aucun COALESCE implicite).
        /// </summary>
        public static DateTime? DateReferencePour(int mvType, int mvPoint, DateTime? mvPointDate, DateTime mvDate)
            => (mvType != 0 && mvPoint == 1) ? mvPointDate : mvDate;

        /// <summary>Gate déclaration : espèce (MV_Type=0, auto-rapprochée) OU rapproché banque (MV_Point=1).</summary>
        public static bool EstDeclarable(int mvType, int mvPoint)
            => mvType == 0 || mvPoint == 1;

        /// <summary>
        /// Fragment SQL de <see cref="DateReferencePour"/> pour l'alias RT_MOUVEMENT <c>M</c>.
        /// Réplique à l'IDENTIQUE la dérivation C#.
        /// </summary>
        public const string DateReferenceSqlM =
            "(CASE WHEN M.MV_Type <> 0 AND M.MV_Point = 1 THEN M.MV_PointDate ELSE M.MV_Date END)";

        /// <summary>Fragment SQL de <see cref="EstDeclarable"/> pour l'alias RT_MOUVEMENT <c>M</c>.</summary>
        public const string EstDeclarableSqlM = "(M.MV_Type = 0 OR M.MV_Point = 1)";
    }
}
