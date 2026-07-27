using System;
using System.Collections.Generic;
using System.Linq;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : type de convention, reproduit à
    /// l'identique le legacy <c>TypeConvention</c>
    /// (<c>Tresorerie.Core/Models/ConventionDelaisPaiementTiers.cs:31-37</c>) : Convention (plage
    /// DateDebut/DateFin) = 0, Facture (dérogation ponctuelle) = 1.
    /// </summary>
    public enum TypeConventionDelaiPaiement
    {
        Convention = 0,
        Facture = 1
    }

    /// <summary>
    /// TASK-129 : résumé minimal d'une convention existante, utilisé UNIQUEMENT pour le contrôle de
    /// chevauchement (message d'erreur explicite citant la convention en conflit).
    /// </summary>
    public sealed class ConventionExistanteResume
    {
        public int CpId { get; init; }
        public string Numero { get; init; } = string.Empty;
        public DateTime DateDebut { get; init; }
        public DateTime DateFin { get; init; }
    }

    /// <summary>
    /// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : règles de validation PURES (hors
    /// DB), reproduisant à l'identique <c>SocieteManager.Complement.cs:471-589</c>
    /// (<c>ConventionDelaisPaiementTiersCreate</c>/<c>Terminer</c>), avec UNE correction actée PO
    /// (19/07/2026) : le contrôle de chevauchement, incomplet dans le legacy (<c>// TODO: verifier le
    /// chauvochement des date</c>, l.531 — ne testait que la date de début de la nouvelle convention
    /// contre la plage d'une convention existante), devient bidirectionnel et standard.
    /// </summary>
    public static class ConventionDelaiPaiementValidator
    {
        /// <summary>Plafond legacy (l.491-492) : jamais dépassé, contrôle applicatif (pas de contrainte base).</summary>
        public const int PlafondJours = 180;

        /// <summary>
        /// Reproduit legacy l.490-492 : le délai doit être strictement positif et ne doit pas dépasser
        /// <see cref="PlafondJours"/> jours.
        /// </summary>
        public static void ValiderPlafond(int nombreJoursDelaisPaiement)
        {
            if (nombreJoursDelaisPaiement <= 0)
                throw new ArgumentException("Le délai de paiement doit être strictement positif.", nameof(nombreJoursDelaisPaiement));

            if (nombreJoursDelaisPaiement > PlafondJours)
                throw new InvalidOperationException($"Le délai de paiement ne doit pas dépasser {PlafondJours} jours.");
        }

        /// <summary>Reproduit legacy l.508-509 (type Convention) : DateFin doit être &gt;= DateDebut.</summary>
        public static void ValiderDatesConvention(DateTime dateDebut, DateTime dateFin)
        {
            if (dateFin.Date < dateDebut.Date)
                throw new InvalidOperationException("La date fin doit être supérieure ou égale à la date début.");
        }

        /// <summary>
        /// Contrôle de chevauchement CORRIGÉ (décision PO 19/07/2026, remplace le contrôle legacy
        /// incomplet l.511) : bidirectionnel, standard —
        /// chevauchement si (nouvelle.DateDebut &lt;= existante.DateFin) ET (existante.DateDebut &lt;=
        /// nouvelle.DateFin). Couvre les 4 configurations (contenue, englobante, partielle gauche,
        /// partielle droite), là où le legacy ne testait que si la date de début de la nouvelle
        /// convention tombait dans la plage d'une convention existante (laissant passer, par exemple,
        /// une nouvelle convention qui démarre avant une existante et la recouvre entièrement).
        /// </summary>
        /// <returns>La première convention existante en conflit, ou <c>null</c> si aucun chevauchement.</returns>
        public static ConventionExistanteResume? TrouverChevauchement(
            DateTime nouvelleDateDebut,
            DateTime nouvelleDateFin,
            IEnumerable<ConventionExistanteResume> conventionsExistantes)
        {
            if (conventionsExistantes == null) throw new ArgumentNullException(nameof(conventionsExistantes));

            var debut = nouvelleDateDebut.Date;
            var fin = nouvelleDateFin.Date;

            return conventionsExistantes.FirstOrDefault(x =>
                debut <= x.DateFin.Date && x.DateDebut.Date <= fin);
        }

        /// <summary>Reproduit legacy l.526-528 (type Facture) : une seule convention par facture (CP_FactureNo).</summary>
        public static void ValiderUniciteFacture(int factureNo, IEnumerable<int?> facturesNoExistantes)
        {
            if (facturesNoExistantes == null) throw new ArgumentNullException(nameof(facturesNoExistantes));

            if (facturesNoExistantes.Any(f => f.HasValue && f.Value == factureNo))
                throw new InvalidOperationException("Il existe une convention pour la même facture.");
        }

        /// <summary>
        /// Reproduit legacy l.579-583 (<c>ConventionDelaisPaiementTiersTerminer</c>) : la nouvelle
        /// DateFin (clôture anticipée) doit être comprise entre DateDebut et l'ancienne DateFin.
        /// </summary>
        public static void ValiderTerminer(DateTime dateDebut, DateTime ancienneDateFin, DateTime nouvelleDateFin)
        {
            if (nouvelleDateFin.Date < dateDebut.Date)
                throw new InvalidOperationException("La date fin doit être supérieure ou égale à la date début.");

            if (nouvelleDateFin.Date > ancienneDateFin.Date)
                throw new InvalidOperationException("La date fin doit être inférieure ou égale à la date fin actuelle.");
        }
    }
}
