using System;
using System.Collections.Generic;
using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-129 — Délai de Paiement Maroc, Convention par tiers. Vérifie
    /// <see cref="ConventionDelaiPaiementValidator"/> : plafond 180 jours, contrôle de chevauchement
    /// bidirectionnel CORRIGÉ (4 configurations : contenue, englobante, partielle gauche, partielle
    /// droite — le legacy ne couvrait aucune de ces 4 correctement, cf. <c>// TODO: verifier le
    /// chauvochement des date</c>, l.531), unicité facture, bornes de la clôture anticipée. Tests purs,
    /// hors base.
    /// </summary>
    public class ConventionDelaiPaiementValidatorTests
    {
        // ── Plafond 180 jours (legacy l.490-492) ──────────────────────────────────

        [Fact]
        public void ValiderPlafond_180Jours_NeLeveRien()
        {
            var ex = Record.Exception(() => ConventionDelaiPaiementValidator.ValiderPlafond(180));
            Assert.Null(ex);
        }

        [Fact]
        public void ValiderPlafond_181Jours_Leve()
        {
            Assert.Throws<InvalidOperationException>(() => ConventionDelaiPaiementValidator.ValiderPlafond(181));
        }

        [Fact]
        public void ValiderPlafond_Zero_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ConventionDelaiPaiementValidator.ValiderPlafond(0));
        }

        [Fact]
        public void ValiderPlafond_Negatif_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ConventionDelaiPaiementValidator.ValiderPlafond(-10));
        }

        // ── Dates convention (DateFin >= DateDebut, legacy l.508-509) ─────────────

        [Fact]
        public void ValiderDatesConvention_FinAvantDebut_Leve()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ConventionDelaiPaiementValidator.ValiderDatesConvention(new DateTime(2026, 2, 1), new DateTime(2026, 1, 31)));
        }

        [Fact]
        public void ValiderDatesConvention_FinEgaleDebut_NeLeveRien()
        {
            var ex = Record.Exception(() =>
                ConventionDelaiPaiementValidator.ValiderDatesConvention(new DateTime(2026, 2, 1), new DateTime(2026, 2, 1)));
            Assert.Null(ex);
        }

        // ── Chevauchement bidirectionnel CORRIGÉ — les 4 configurations ──────────

        private static ConventionExistanteResume Existante(DateTime debut, DateTime fin, string numero = "CONV-EXISTANTE") =>
            new() { CpId = 1, Numero = numero, DateDebut = debut, DateFin = fin };

        [Fact]
        public void Chevauchement_NouvelleContenueDansExistante_Detecte()
        {
            // Existante 01/01 → 31/03, nouvelle 01/02 → 28/02 (entièrement contenue).
            var existante = Existante(new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), new[] { existante });

            Assert.NotNull(conflit);
            Assert.Equal("CONV-EXISTANTE", conflit!.Numero);
        }

        [Fact]
        public void Chevauchement_NouvelleEnglobeExistante_Detecte()
        {
            // Trou EXACT du legacy (exemple TASK) : existante 01/02 → 28/02, nouvelle 01/01 → 31/03
            // (démarre avant l'existante et la recouvre entièrement). Le legacy (l.511) ne testait
            // que si la date de DÉBUT de la nouvelle tombait dans la plage existante : 01/01
            // n'appartient pas à [01/02, 28/02] → laissait passer à tort. Le contrôle corrigé doit
            // détecter ce cas.
            var existante = Existante(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), new[] { existante });

            Assert.NotNull(conflit);
        }

        [Fact]
        public void Chevauchement_PartielGauche_Detecte()
        {
            // Existante 01/02 → 28/02, nouvelle 15/01 → 15/02 (déborde à gauche, chevauche la fin gauche de l'existante).
            var existante = Existante(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 1, 15), new DateTime(2026, 2, 15), new[] { existante });

            Assert.NotNull(conflit);
        }

        [Fact]
        public void Chevauchement_PartielDroite_Detecte()
        {
            // Existante 01/02 → 28/02, nouvelle 15/02 → 15/03 (déborde à droite).
            var existante = Existante(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 2, 15), new DateTime(2026, 3, 15), new[] { existante });

            Assert.NotNull(conflit);
        }

        [Fact]
        public void Chevauchement_AucuneIntersection_NeDetectePasDeConflit()
        {
            // Écart franc entre les deux périodes (mois de mars entier non couvert par aucune des deux).
            var existante = Existante(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), new[] { existante });

            Assert.Null(conflit);
        }

        [Fact]
        public void Chevauchement_Adjacentes_NeDetectePasDeConflit()
        {
            // Nouvelle démarre le lendemain de la fin de l'existante : pas de chevauchement.
            var existante = Existante(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), new[] { existante });

            Assert.Null(conflit);
        }

        // ── Unicité facture (legacy l.526-528) ────────────────────────────────────

        [Fact]
        public void ValiderUniciteFacture_FactureDejaUtilisee_Leve()
        {
            var facturesExistantes = new int?[] { 10, 42, null };
            Assert.Throws<InvalidOperationException>(() =>
                ConventionDelaiPaiementValidator.ValiderUniciteFacture(42, facturesExistantes));
        }

        [Fact]
        public void ValiderUniciteFacture_FactureNonUtilisee_NeLeveRien()
        {
            var facturesExistantes = new int?[] { 10, null };
            var ex = Record.Exception(() =>
                ConventionDelaiPaiementValidator.ValiderUniciteFacture(42, facturesExistantes));
            Assert.Null(ex);
        }

        // ── Clôture anticipée (Terminer, legacy l.579-583) ────────────────────────

        [Fact]
        public void ValiderTerminer_NouvelleDateFinAvantDebut_Leve()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ConventionDelaiPaiementValidator.ValiderTerminer(
                    dateDebut: new DateTime(2026, 1, 1),
                    ancienneDateFin: new DateTime(2026, 12, 31),
                    nouvelleDateFin: new DateTime(2025, 12, 31)));
        }

        [Fact]
        public void ValiderTerminer_NouvelleDateFinApresAncienne_Leve()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ConventionDelaiPaiementValidator.ValiderTerminer(
                    dateDebut: new DateTime(2026, 1, 1),
                    ancienneDateFin: new DateTime(2026, 12, 31),
                    nouvelleDateFin: new DateTime(2027, 1, 1)));
        }

        [Fact]
        public void ValiderTerminer_NouvelleDateFinDansLaPlage_NeLeveRien()
        {
            var ex = Record.Exception(() =>
                ConventionDelaiPaiementValidator.ValiderTerminer(
                    dateDebut: new DateTime(2026, 1, 1),
                    ancienneDateFin: new DateTime(2026, 12, 31),
                    nouvelleDateFin: new DateTime(2026, 6, 30)));
            Assert.Null(ex);
        }

        [Fact]
        public void ValiderTerminer_NouvelleDateFinEgaleDateDebut_NeLeveRien()
        {
            var ex = Record.Exception(() =>
                ConventionDelaiPaiementValidator.ValiderTerminer(
                    dateDebut: new DateTime(2026, 1, 1),
                    ancienneDateFin: new DateTime(2026, 12, 31),
                    nouvelleDateFin: new DateTime(2026, 1, 1)));
            Assert.Null(ex);
        }
    }
}
