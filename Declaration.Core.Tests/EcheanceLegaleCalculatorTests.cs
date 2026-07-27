using System;
using System.Collections.Generic;
using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-127 — Socle Délai de Paiement Maroc. Vérifie que <see cref="EcheanceLegaleCalculator"/>
    /// reproduit à l'identique <c>EcheancePaiementCalculator.GetDelaisPaiementTiers</c> (résolution du
    /// délai + décalage jour ouvré) et expose l'origine du délai. Tests purs, hors base — les données
    /// (conventions, jours de repos, défaut société) sont fournies explicitement. Dates réelles 2025
    /// (jours ouvrés vérifiés) pour la crédibilité du rejeu contre les données réelles.
    /// </summary>
    public class EcheanceLegaleCalculatorTests
    {
        private static readonly IReadOnlyCollection<ConventionDelaiPaiement> AucuneConvention =
            Array.Empty<ConventionDelaiPaiement>();

        private static readonly IReadOnlyCollection<DateTime> AucunJourRepos =
            Array.Empty<DateTime>();

        // ── Résolution du délai ───────────────────────────────────────────────────

        [Fact]
        public void Defaut_AucuneConvention_AppliqueDelaiSociete()
        {
            // 2025-06-02 (lundi) + 60 j = 2025-08-01 (vendredi), aucun décalage requis.
            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 42,
                documentNumero: "FA-001",
                conventions: AucuneConvention,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(60, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 8, 1), r.EcheanceLegale);
            Assert.Equal(OrigineDelai.Defaut, r.OrigineDelai);
        }

        [Fact]
        public void Convention_Intervalle_ActiveSurLaDateDocument_EstAppliquee()
        {
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    FactureNumero = null,
                    DateDebut = new DateTime(2025, 1, 1),
                    DateFin = new DateTime(2025, 12, 31),
                    NombreJoursDelaisPaiement = 30
                }
            };

            // 2025-06-02 (lundi) + 30 j = 2025-07-02 (mercredi).
            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 5,
                documentNumero: "FA-XYZ",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(30, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 7, 2), r.EcheanceLegale);
            Assert.Equal(OrigineDelai.Convention, r.OrigineDelai);
        }

        [Fact]
        public void ConventionFacture_Exacte_EstAppliquee()
        {
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    FactureNumero = "FA-001",
                    NombreJoursDelaisPaiement = 15
                }
            };

            // 2025-06-02 (lundi) + 15 j = 2025-06-17 (mardi).
            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 5,
                documentNumero: "FA-001",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(15, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 6, 17), r.EcheanceLegale);
            Assert.Equal(OrigineDelai.ConventionFacture, r.OrigineDelai);
        }

        [Fact]
        public void Priorite_Facture_LEmporteSur_Convention_QuiLEmporteSur_Defaut()
        {
            // Les trois sources sont applicables simultanément : la convention Facture (15 j) doit
            // primer sur la convention intervalle (30 j), elle-même prioritaire sur le défaut (60 j).
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 7,
                    FactureNumero = null,
                    DateDebut = new DateTime(2025, 1, 1),
                    DateFin = new DateTime(2025, 12, 31),
                    NombreJoursDelaisPaiement = 30
                },
                new ConventionDelaiPaiement
                {
                    TiersNo = 7,
                    FactureNumero = "FA-001",
                    NombreJoursDelaisPaiement = 15
                }
            };

            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 7,
                documentNumero: "FA-001",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(15, r.NombreJoursApplique);
            Assert.Equal(OrigineDelai.ConventionFacture, r.OrigineDelai);
        }

        [Fact]
        public void Convention_AutreTiers_NEstPasAppliquee_ReplieSurDefaut()
        {
            // La convention concerne le tiers 5, le document concerne le tiers 999 → défaut société.
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    DateDebut = new DateTime(2025, 1, 1),
                    DateFin = new DateTime(2025, 12, 31),
                    NombreJoursDelaisPaiement = 30
                }
            };

            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 999,
                documentNumero: "FA-001",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(60, r.NombreJoursApplique);
            Assert.Equal(OrigineDelai.Defaut, r.OrigineDelai);
        }

        // ── Décalage jour ouvré ───────────────────────────────────────────────────

        [Fact]
        public void Decalage_Weekend_Seul_SamediPuisDimanche_TombeAuLundi()
        {
            // 2025-06-02 (lundi) + 5 j = 2025-06-07 (samedi) → +1 dimanche 06-08 → +1 lundi 06-09.
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    DateDebut = new DateTime(2025, 1, 1),
                    DateFin = new DateTime(2025, 12, 31),
                    NombreJoursDelaisPaiement = 5
                }
            };

            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2),
                tiersNo: 5,
                documentNumero: "FA",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(7, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 6, 9), r.EcheanceLegale);
        }

        [Fact]
        public void Decalage_JourFerie_Seul_JourOuvre_EstDecaleAuLendemainOuvre()
        {
            // 2025-04-01 (mardi) + 30 j = 2025-05-01 (jeudi, férié) → +1 = 2025-05-02 (vendredi ouvré).
            var joursRepos = new[] { new DateTime(2025, 5, 1) }; // jeudi férié
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    DateDebut = new DateTime(2025, 1, 1),
                    DateFin = new DateTime(2025, 12, 31),
                    NombreJoursDelaisPaiement = 30
                }
            };

            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 4, 1),
                tiersNo: 5,
                documentNumero: "FA",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: joursRepos);

            Assert.Equal(31, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 5, 2), r.EcheanceLegale);
        }

        [Fact]
        public void Decalage_Cumule_FerieTombantSurLeNouveauSamedi_RetestDepuisDateDocument()
        {
            // Cas clé : deux fériés consécutifs suivis d'un week-end, dont un férié qui tombe un samedi.
            // 2025-04-30 (mercredi) + 1 j = 05-01 (jeudi, férié) → +1 = 05-02 (vendredi, férié)
            //   → +1 = 05-03 (samedi, week-end ET dans P_JOURSREPOS) → +1 = 05-04 (dimanche)
            //   → +1 = 05-05 (lundi ouvré). nbJours final = 5, échéance 2025-05-05.
            // Vérifie que chaque décalage réincrémente nbJours ET recalcule depuis DateDocument :
            // date et nombre de jours restent cohérents (contrat legacy documentDate.AddDays(++nbJours)).
            var joursRepos = new[]
            {
                new DateTime(2025, 5, 1), // jeudi férié
                new DateTime(2025, 5, 2), // vendredi férié
                new DateTime(2025, 5, 3)  // samedi ET férié (le "nouveau samedi calculé")
            };
            var conventions = new[]
            {
                new ConventionDelaiPaiement
                {
                    TiersNo = 5,
                    FactureNumero = "FA-001",
                    NombreJoursDelaisPaiement = 1
                }
            };

            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 4, 30),
                tiersNo: 5,
                documentNumero: "FA-001",
                conventions: conventions,
                nombreJoursDefautSociete: 60,
                joursRepos: joursRepos);

            Assert.Equal(5, r.NombreJoursApplique);
            Assert.Equal(new DateTime(2025, 5, 5), r.EcheanceLegale);
            Assert.Equal(OrigineDelai.ConventionFacture, r.OrigineDelai);
        }

        [Fact]
        public void PartieHeure_DeLaDateDocument_EstIgnoree()
        {
            // La composante heure ne doit pas influencer le calcul (legacy travaille sur .Date).
            var r = EcheanceLegaleCalculator.Calculer(
                dateDocument: new DateTime(2025, 6, 2, 14, 30, 0),
                tiersNo: 42,
                documentNumero: "FA",
                conventions: AucuneConvention,
                nombreJoursDefautSociete: 60,
                joursRepos: AucunJourRepos);

            Assert.Equal(new DateTime(2025, 8, 1), r.EcheanceLegale);
        }
    }
}
