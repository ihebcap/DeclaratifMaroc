using System;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-128 : statut de bascule d'une échéance vis-à-vis de la date de mise en route du module
    /// Délai de Paiement Maroc pour sa société. Consommé par TASK-131 (sélection des lignes hors
    /// délai) pour décider si le calcul automatique du retard s'applique.
    /// </summary>
    public enum StatutBasculeEcheance
    {
        /// <summary>
        /// Le calcul automatique du retard s'applique normalement : soit l'échéance légale est
        /// postérieure ou égale à la date de mise en route de sa société, soit l'échéance a déjà
        /// une ligne historique dans <c>RT_DECLARATIONDELAISPAIEMENTLG</c> (donc pas "jamais
        /// déclarée").
        /// </summary>
        CalculAutomatique,

        /// <summary>
        /// Échéance antérieure à la mise en route de sa société, jamais déclarée (ni dans ce
        /// système ni dans l'ancien) et sans reprise manuelle saisie. Statut affiché à l'écran de
        /// contrôle (TASK-134) : "Antérieure à la mise en route — retard réel inconnu". EXCLUE par
        /// défaut du calcul automatique, non intégrable à une déclaration tant que la reprise n'a
        /// pas été faite. Jamais supprimée silencieusement.
        /// </summary>
        AnterieureRetardInconnu,

        /// <summary>
        /// Échéance antérieure à la mise en route de sa société, mais une reprise manuelle
        /// ("retard déjà connu/déclaré jusqu'au [date]") a été saisie pour cette échéance précise
        /// (<c>DM_REPRISE_DELAIPAIEMENT</c>). Rentre dans le calcul automatique normal pour les
        /// périodes suivantes, avec <see cref="ResultatBasculeEcheance.BorneReprise"/> comme borne
        /// d'ouverture (équivalent d'un solde d'ouverture comptable).
        /// </summary>
        AnterieureAvecRepriseSaisie
    }

    /// <summary>
    /// TASK-128 : résultat de la résolution du statut de bascule d'une échéance.
    /// </summary>
    public sealed class ResultatBasculeEcheance
    {
        public StatutBasculeEcheance Statut { get; init; }

        /// <summary>
        /// Borne d'ouverture du calcul incrémental futur pour cette échéance précise, renseignée
        /// UNIQUEMENT quand <see cref="Statut"/> == <see cref="StatutBasculeEcheance.AnterieureAvecRepriseSaisie"/>
        /// (valeur = <c>DM_REPRISE_DELAIPAIEMENT.DateDejaDeclareeJusquau</c>). Null dans les deux
        /// autres cas : pour <see cref="StatutBasculeEcheance.CalculAutomatique"/>, TASK-131 pilote
        /// elle-même le calcul standard depuis l'échéance légale (hors périmètre de ce garde-fou) ;
        /// pour <see cref="StatutBasculeEcheance.AnterieureRetardInconnu"/>, aucune borne n'existe
        /// tant que la reprise n'a pas été saisie.
        /// </summary>
        public DateTime? BorneReprise { get; init; }
    }

    /// <summary>
    /// TASK-128 — Garde-fou de bascule "date de mise en route" du module Délai de Paiement Maroc.
    /// Calculateur PUR (hors DB), testable comme <see cref="EcheanceLegaleCalculator"/>. Décide,
    /// pour UNE échéance déjà résolue (échéance légale connue), si le calcul automatique du retard
    /// déclaré (porté par TASK-131) doit s'appliquer ou si une reprise manuelle est requise.
    ///
    /// Règle de bascule (décision PO 19/07/2026, cf. TASK-128) :
    /// - Pas de date de mise en route configurée pour la société ("pas encore configuré", jamais
    ///   une date arbitraire) => calcul automatique DÉSACTIVÉ pour toute échéance de cette société,
    ///   quelle que soit sa date ou son historique — décision explicite du PO, pas une omission.
    /// - Échéance légale &gt;= date de mise en route (ou historique déjà connu dans
    ///   <c>RT_DECLARATIONDELAISPAIEMENTLG</c>, table partagée/réutilisée telle quelle avec l'ancien
    ///   système) => calcul automatique normal.
    /// - Échéance légale &lt; date de mise en route ET aucun historique => EXCLUE (retard réel
    ///   inconnu) tant qu'aucune reprise manuelle n'a été saisie pour cette échéance précise ; une
    ///   fois saisie, la borne de reprise devient le point de départ du calcul incrémental futur.
    ///
    /// Ce calculateur ne fait AUCUNE lecture DB : les entrées (échéance légale, date de mise en
    /// route, indicateur d'historique legacy, éventuelle reprise saisie) sont résolues par
    /// l'appelant (service d'orchestration TASK-128 pour ses deux premières, TASK-131 pour
    /// l'indicateur d'historique — hors périmètre STRICT de cette tâche, cf. TASK-128 §Périmètre).
    /// </summary>
    public static class DelaiPaiementBootstrapGuard
    {
        /// <summary>
        /// Résout le statut de bascule d'une échéance.
        /// </summary>
        /// <param name="echeanceLegale">Échéance légale déjà calculée (ex. <see cref="EcheanceLegaleCalculator"/>). Seule la partie Date est utilisée.</param>
        /// <param name="dateMiseEnRouteSociete">Date de mise en route du module pour la société de l'échéance ; null = pas encore configurée.</param>
        /// <param name="aHistoriqueDeclarationLegacy">True si cette échéance a déjà au moins une ligne dans <c>RT_DECLARATIONDELAISPAIEMENTLG</c> (jamais "jamais déclarée").</param>
        /// <param name="dateDejaDeclareeJusquau">Borne saisie par une reprise manuelle (<c>DM_REPRISE_DELAIPAIEMENT.DateDejaDeclareeJusquau</c>), si elle existe pour cette échéance.</param>
        public static ResultatBasculeEcheance Resoudre(
            DateTime echeanceLegale,
            DateTime? dateMiseEnRouteSociete,
            bool aHistoriqueDeclarationLegacy,
            DateTime? dateDejaDeclareeJusquau)
        {
            // Pas de date de mise en route configurée : "aucune bascule à gérer" tant que le PO
            // n'a pas saisi la date -- calcul automatique désactivé par défaut plutôt qu'une date
            // arbitraire (cf. TASK-128 §Étapes 2).
            if (dateMiseEnRouteSociete == null)
            {
                return new ResultatBasculeEcheance
                {
                    Statut = StatutBasculeEcheance.AnterieureRetardInconnu,
                    BorneReprise = null
                };
            }

            if (aHistoriqueDeclarationLegacy || echeanceLegale.Date >= dateMiseEnRouteSociete.Value.Date)
            {
                return new ResultatBasculeEcheance
                {
                    Statut = StatutBasculeEcheance.CalculAutomatique,
                    BorneReprise = null
                };
            }

            if (dateDejaDeclareeJusquau.HasValue)
            {
                return new ResultatBasculeEcheance
                {
                    Statut = StatutBasculeEcheance.AnterieureAvecRepriseSaisie,
                    BorneReprise = dateDejaDeclareeJusquau.Value.Date
                };
            }

            return new ResultatBasculeEcheance
            {
                Statut = StatutBasculeEcheance.AnterieureRetardInconnu,
                BorneReprise = null
            };
        }
    }
}
