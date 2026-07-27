using System;
using System.Collections.Generic;
using System.Linq;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-127 (socle Délai de Paiement Maroc) : domaine d'une convention de délai de paiement.
    /// Reproduit la sémantique legacy <c>DomaineConvention</c> (Fournisseur/Client) mais nommée
    /// côté métier GRF Achat/Vente (objectif TASK-127). Mapping vers la colonne Sage
    /// <c>RT_CONVENTIONTIERS.CP_Domaine</c> (0 = Fournisseur/Achat, 1 = Client/Vente) : à la charge
    /// du repository de conventions livré en TASK-129 — le calculateur ne l'utilise que pour recevoir
    /// des conventions déjà filtrées par domaine, comme le fait le legacy
    /// (<c>ConventionDelaisPaiementTiersGetAll(domaine)</c>).
    /// </summary>
    public enum DomaineDelaiPaiement
    {
        Achat = 0,
        Vente = 1
    }

    /// <summary>
    /// TASK-127 : origine du délai finalement appliqué. Ajout GRF (absent du legacy, qui ne retournait
    /// qu'un nombre de jours) pour la traçabilité UI (TASK-134/135) — principe « aucune ligne
    /// silencieuse » déjà appliqué au module TVA.
    /// </summary>
    public enum OrigineDelai
    {
        /// <summary>Convention de type Facture exacte (TiersNo + FactureNumero == numéro du document).</summary>
        ConventionFacture,
        /// <summary>Convention de type Convention dont l'intervalle DateDebut..DateFin couvre la date du document.</summary>
        Convention,
        /// <summary>Aucune convention applicable : délai par défaut société (SO_NbJoursDelaiPaiement).</summary>
        Defaut
    }

    /// <summary>
    /// TASK-127 : contrat d'entrée MINIMAL d'une convention de délai de paiement, tel que consommé par
    /// le calculateur. Volontairement réduit aux seuls champs utilisés par la résolution (le repository
    /// livré en TASK-129 projette ses lignes <c>RT_CONVENTIONTIERS</c> vers ce type). Le type de
    /// convention (Convention/Facture) n'est PAS un champ ici : le legacy ne teste jamais le type
    /// explicitement — il distingue Facture d'une part (présence d'un <see cref="FactureNumero"/> égal
    /// au numéro du document) et Convention d'autre part (intervalle <see cref="DateDebut"/>..
    /// <see cref="DateFin"/>). Reproduire ce comportement à l'identique impose de NE PAS filtrer sur un
    /// champ Type.
    /// </summary>
    public sealed class ConventionDelaiPaiement
    {
        public int TiersNo { get; init; }
        public string? FactureNumero { get; init; }
        public DateTime? DateDebut { get; init; }
        public DateTime? DateFin { get; init; }
        public int NombreJoursDelaisPaiement { get; init; }
    }

    /// <summary>
    /// TASK-127 : résultat de la résolution du délai + calcul de l'échéance légale.
    /// </summary>
    public sealed class ResultatDelaiPaiement
    {
        /// <summary>Nombre de jours effectivement appliqué (délai résolu + décalages jours ouvrés cumulés).</summary>
        public int NombreJoursApplique { get; init; }

        /// <summary>Date d'échéance légale = DateDocument + NombreJoursApplique (jour ouvré garanti).</summary>
        public DateTime EcheanceLegale { get; init; }

        /// <summary>Origine du délai résolu (traçabilité).</summary>
        public OrigineDelai OrigineDelai { get; init; }
    }

    /// <summary>
    /// TASK-127 — Socle Délai de Paiement Maroc : point UNIQUE et pur (hors DB) de résolution du délai
    /// de paiement applicable et de calcul de l'échéance légale. Reproduit à l'identique
    /// <c>EcheancePaiementCalculator.GetDelaisPaiementTiers</c>
    /// (<c>Tresorerie.UICommun/Helper/EcheancePaiementCalculator.cs:21-56</c>), et ajoute
    /// <see cref="OrigineDelai"/> pour la traçabilité.
    ///
    /// Consommé par le service d'orchestration <c>DelaiPaiementService</c> (Declaration.Application),
    /// lui-même destiné aux TASK-129/131/135. Les données (conventions déjà filtrées par domaine,
    /// délai par défaut société, jours de repos) sont lues par les repositories et passées en entrée :
    /// ce calculateur reste testable hors base.
    /// </summary>
    public static class EcheanceLegaleCalculator
    {
        /// <summary>
        /// Résout le délai applicable puis calcule l'échéance légale.
        /// </summary>
        /// <param name="dateDocument">Date du document (facture). Seule la partie Date est utilisée.</param>
        /// <param name="tiersNo">Identifiant du tiers (CT_No).</param>
        /// <param name="documentNumero">Numéro du document (utilisé pour l'appariement convention Facture).</param>
        /// <param name="conventions">
        /// Conventions actives déjà filtrées par domaine (comme le legacy qui charge
        /// <c>ConventionDelaisPaiementTiersGetAll(domaine)</c>). Peut être vide.
        /// </param>
        /// <param name="nombreJoursDefautSociete">
        /// Délai par défaut société (<c>P_SOCIETE.SO_NbJoursDelaiPaiement</c>, amorçage 60), appliqué
        /// quand aucune convention ne correspond.
        /// </param>
        /// <param name="joursRepos">
        /// Jours de repos de la société (<c>P_JOURSREPOS.JR_Date</c>). Seule la partie Date est utilisée.
        /// </param>
        public static ResultatDelaiPaiement Calculer(
            DateTime dateDocument,
            int tiersNo,
            string? documentNumero,
            IReadOnlyCollection<ConventionDelaiPaiement> conventions,
            int nombreJoursDefautSociete,
            IReadOnlyCollection<DateTime> joursRepos)
        {
            if (conventions == null) throw new ArgumentNullException(nameof(conventions));
            if (joursRepos == null) throw new ArgumentNullException(nameof(joursRepos));

            // ── 1. Résolution du délai (priorité : Facture exacte > Convention (intervalle) > défaut).
            // Reproduit EXACTEMENT les deux FirstOrDefault du legacy (l.35-42) : l'appariement Facture
            // repose sur l'égalité stricte FactureNumero == documentNumero (comparaison ordinale, comme
            // l'opérateur == du legacy — y compris null == null), et JAMAIS sur un champ Type.
            OrigineDelai origine;
            int nbJours;

            var conventionFacture = conventions.FirstOrDefault(x =>
                x.TiersNo == tiersNo &&
                string.Equals(x.FactureNumero, documentNumero, StringComparison.Ordinal));

            if (conventionFacture != null)
            {
                origine = OrigineDelai.ConventionFacture;
                nbJours = conventionFacture.NombreJoursDelaisPaiement;
            }
            else
            {
                var conventionIntervalle = conventions.FirstOrDefault(x =>
                    x.TiersNo == tiersNo &&
                    x.DateDebut.HasValue && x.DateFin.HasValue &&
                    x.DateDebut.Value.Date <= dateDocument.Date &&
                    x.DateFin.Value.Date >= dateDocument.Date);

                if (conventionIntervalle != null)
                {
                    origine = OrigineDelai.Convention;
                    nbJours = conventionIntervalle.NombreJoursDelaisPaiement;
                }
                else
                {
                    origine = OrigineDelai.Defaut;
                    nbJours = nombreJoursDefautSociete;
                }
            }

            // ── 2. Échéance légale : DateDocument + nbJours, décalée au jour ouvré suivant.
            // Sémantique legacy stricte (l.47-54) : tant que la date tombe un jour de P_JOURSREPOS OU un
            // samedi OU un dimanche, on incrémente nbJours de 1 et on RECALCULE DEPUIS DateDocument
            // (documentDate.AddDays(++nbJours)) — jamais depuis la dernière date testée. nbJours et la
            // date restent ainsi toujours cohérents (le contrat legacy retourne le nombre de jours).
            var datePaiement = dateDocument.Date.AddDays(nbJours);
            while (joursRepos.Any(j => j.Date == datePaiement.Date)
                   || datePaiement.DayOfWeek == DayOfWeek.Saturday
                   || datePaiement.DayOfWeek == DayOfWeek.Sunday)
            {
                nbJours++;
                datePaiement = dateDocument.Date.AddDays(nbJours);
            }

            return new ResultatDelaiPaiement
            {
                NombreJoursApplique = nbJours,
                EcheanceLegale = datePaiement,
                OrigineDelai = origine
            };
        }
    }
}
