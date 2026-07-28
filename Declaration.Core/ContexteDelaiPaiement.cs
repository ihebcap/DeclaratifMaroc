using System;
using System.Collections.Generic;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-131 : référentiel de calcul du délai de paiement (TASK-127) chargé UNE SEULE FOIS pour
    /// une société/domaine, puis réutilisé pour résoudre l'échéance légale de N échéances.
    ///
    /// Raison d'être : la sélection DDP résout l'échéance légale de plusieurs centaines/milliers
    /// d'échéances en une passe. Passer par <c>IDelaiPaiementService.ResoudreDelaiAsync</c> par
    /// échéance provoquerait 3 requêtes SQL PAR échéance (conventions + jours de repos + délai défaut)
    /// — un N+1 inacceptable. Ce contexte transporte les 3 lectures et délègue le métier au SEUL
    /// point de vérité <see cref="EcheanceLegaleCalculator"/> : aucune logique dupliquée.
    /// </summary>
    public sealed class ContexteDelaiPaiement
    {
        /// <summary>Conventions actives de la société, DÉJÀ filtrées par domaine (comme le legacy).</summary>
        public IReadOnlyCollection<ConventionDelaiPaiement> Conventions { get; init; } = Array.Empty<ConventionDelaiPaiement>();

        /// <summary>Délai par défaut société (<c>P_SOCIETE.SO_NbJoursDelaiPaiement</c>).</summary>
        public int NombreJoursDefautSociete { get; init; }

        /// <summary>Jours de repos de la société (<c>P_JOURSREPOS.JR_Date</c>).</summary>
        public IReadOnlyCollection<DateTime> JoursRepos { get; init; } = Array.Empty<DateTime>();

        /// <summary>
        /// Résout le délai applicable et l'échéance légale d'un document, avec la sémantique EXACTE de
        /// <see cref="EcheanceLegaleCalculator.Calculer"/> (TASK-127).
        /// </summary>
        public ResultatDelaiPaiement Resoudre(DateTime dateDocument, int tiersNo, string? documentNumero)
            => EcheanceLegaleCalculator.Calculer(
                dateDocument,
                tiersNo,
                documentNumero,
                Conventions,
                NombreJoursDefautSociete,
                JoursRepos);
    }
}
