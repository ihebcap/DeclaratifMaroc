using System;
using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-128 — Garde-fou de bascule "date de mise en route". Vérifie que
    /// <see cref="DelaiPaiementBootstrapGuard"/> applique exactement la règle décidée par le PO
    /// (19/07/2026, CDC-DELAI-PAIEMENT-MAROC) : aucune échéance antérieure à la mise en route n'est
    /// incluse automatiquement dans un calcul de retard sans reprise manuelle explicite — jamais de
    /// valeur silencieuse. Tests purs, hors base.
    /// </summary>
    public class DelaiPaiementBootstrapGuardTests
    {
        private static readonly DateTime EcheanceLegale = new DateTime(2026, 3, 15);
        private static readonly DateTime MiseEnRoute = new DateTime(2026, 1, 1);

        // ── Société sans date de mise en route configurée ("pas encore configuré") ─────────────

        [Fact]
        public void SansDateMiseEnRoute_EcheancePostérieure_RenvoieAnterieureRetardInconnu_CalculAutoDesactive()
        {
            // Aucune date configurée => "aucune bascule à gérer" => calcul automatique désactivé
            // par défaut (jamais une date arbitraire), MÊME si l'échéance serait par ailleurs
            // "récente" et même sans historique testé ici.
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: EcheanceLegale,
                dateMiseEnRouteSociete: null,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.AnterieureRetardInconnu, r.Statut);
            Assert.Null(r.BorneReprise);
        }

        [Fact]
        public void SansDateMiseEnRoute_MemeAvecHistoriqueLegacy_CalculAutoResteDesactive()
        {
            // Décision assumée (documentée VERIFY) : l'absence de paramétrage société bloque tout
            // calcul automatique, même si l'échéance a par ailleurs un historique — pas de date
            // configurée = société pas encore basculée sur le module.
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: EcheanceLegale,
                dateMiseEnRouteSociete: null,
                aHistoriqueDeclarationLegacy: true,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.AnterieureRetardInconnu, r.Statut);
            Assert.Null(r.BorneReprise);
        }

        // ── Échéance postérieure (ou égale) à la mise en route ──────────────────────────────────

        [Fact]
        public void EcheancePostérieureALaMiseEnRoute_CalculAutomatique()
        {
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: EcheanceLegale, // 2026-03-15 >= 2026-01-01
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.CalculAutomatique, r.Statut);
            Assert.Null(r.BorneReprise);
        }

        [Fact]
        public void EcheanceExactementEgaleALaMiseEnRoute_CalculAutomatique()
        {
            // "antérieure" est STRICT (cf. TASK-128 §Règle) : égalité => pas antérieure => calcul auto.
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: MiseEnRoute,
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.CalculAutomatique, r.Statut);
        }

        [Fact]
        public void PartieHeureIgnoree_TravailSurLaDateSeule()
        {
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: MiseEnRoute.AddHours(23), // même jour, heure différente
                dateMiseEnRouteSociete: MiseEnRoute.AddHours(2),
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.CalculAutomatique, r.Statut);
        }

        // ── Échéance antérieure, avec historique legacy (jamais "jamais déclarée") ──────────────

        [Fact]
        public void EcheanceAnterieure_AvecHistoriqueLegacy_CalculAutomatique_MalgreAbsenceDeReprise()
        {
            var echeanceAnterieure = new DateTime(2025, 6, 1); // < 2026-01-01
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: echeanceAnterieure,
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: true,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.CalculAutomatique, r.Statut);
            Assert.Null(r.BorneReprise);
        }

        // ── Échéance antérieure, jamais déclarée, sans reprise (EXCLUE) ─────────────────────────

        [Fact]
        public void EcheanceAnterieure_SansHistorique_SansReprise_AnterieureRetardInconnu()
        {
            var echeanceAnterieure = new DateTime(2025, 6, 1);
            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: echeanceAnterieure,
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: null);

            Assert.Equal(StatutBasculeEcheance.AnterieureRetardInconnu, r.Statut);
            Assert.Null(r.BorneReprise);
        }

        // ── Échéance antérieure, jamais déclarée, AVEC reprise saisie (INCLUSE, borne = date saisie) ─

        [Fact]
        public void EcheanceAnterieure_SansHistorique_AvecReprise_AnterieureAvecRepriseSaisie_BorneEgaleDateSaisie()
        {
            var echeanceAnterieure = new DateTime(2025, 6, 1);
            var dateDejaDeclareeJusquau = new DateTime(2025, 5, 1);

            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: echeanceAnterieure,
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: dateDejaDeclareeJusquau);

            Assert.Equal(StatutBasculeEcheance.AnterieureAvecRepriseSaisie, r.Statut);
            Assert.Equal(dateDejaDeclareeJusquau, r.BorneReprise);
        }

        [Fact]
        public void EcheanceAnterieure_ReprisePartieHeureIgnoree()
        {
            var echeanceAnterieure = new DateTime(2025, 6, 1);
            var dateDejaDeclareeJusquauAvecHeure = new DateTime(2025, 5, 1, 14, 30, 0);

            var r = DelaiPaiementBootstrapGuard.Resoudre(
                echeanceLegale: echeanceAnterieure,
                dateMiseEnRouteSociete: MiseEnRoute,
                aHistoriqueDeclarationLegacy: false,
                dateDejaDeclareeJusquau: dateDejaDeclareeJusquauAvecHeure);

            Assert.Equal(new DateTime(2025, 5, 1), r.BorneReprise);
        }
    }
}
