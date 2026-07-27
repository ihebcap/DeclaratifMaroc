using System;
using Declaration.Application.Entities;
using Xunit;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-135 — FactureInterrogationRow.AppliquerDelaiPaiement : mesure du délai de
    // paiement fournisseur (CDC §3.3, extension écran Factures, TASK-041).
    //
    // Règle validée (§3.3) :
    //   - facture SOLDÉE (solde restant ≤ 0)      : écart = dernière date de rapprochement
    //     bancaire pertinente − échéance légale. NULL si aucune date de rapprochement connue
    //     (jamais un écart inventé contre une référence absente).
    //   - solde restant > 0 (non payée/partielle) : écart = date du jour − échéance légale
    //     (toujours calculable, provisoire).
    //
    // L'échéance légale elle-même (EcheanceLegaleCalculator) est déjà couverte par
    // Declaration.Core.Tests/EcheanceLegaleCalculatorTests.cs (TASK-127) — ces tests-ci ne
    // couvrent QUE la dérivation de l'écart, logique propre à TASK-135.
    // ═══════════════════════════════════════════════════════════════════════════════

    public class Task135DelaiPaiementFactureTests
    {
        private static FactureInterrogationRow NouvelleLigne(decimal montantTtc, decimal? regle)
        {
            return new FactureInterrogationRow
            {
                EcId = 1,
                DoNumero = "FAC001",
                EcType = 0,
                EcMontant = montantTtc,
                Regle = regle, // agrégat RT_AFFECTATION déjà projeté (cf. GetFacturesInterrogationAsync)
            };
        }

        [Fact]
        public void FactureSoldee_AvecDateRapprochement_EcartCalculeContreCetteDate()
        {
            // TTC = Réglé ⇒ SoldeFacture = 0 ⇒ soldée.
            var row = NouvelleLigne(montantTtc: 1000m, regle: 1000m);
            var echeanceLegale = new DateTime(2026, 05, 01);
            var derniereDateRapprochement = new DateTime(2026, 05, 15); // 14 jours de retard

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement);

            Assert.Equal(echeanceLegale, row.EcheanceLegale);
            Assert.Equal(14, row.EcartJours);
        }

        [Fact]
        public void FactureSoldee_PaieeEnAvance_EcartNegatif()
        {
            var row = NouvelleLigne(montantTtc: 1000m, regle: 1000m);
            var echeanceLegale = new DateTime(2026, 05, 15);
            var derniereDateRapprochement = new DateTime(2026, 05, 01); // 14 jours d'avance

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement);

            Assert.Equal(-14, row.EcartJours);
        }

        [Fact]
        public void FactureSoldee_SansDateRapprochementConnue_EcartNullJamaisInvente()
        {
            // Soldée mais aucune affectation encore rapprochée banque (MV_Point jamais posé) :
            // règle n°1 du projet — jamais un écart inventé contre une référence absente.
            var row = NouvelleLigne(montantTtc: 1000m, regle: 1000m);
            var echeanceLegale = new DateTime(2026, 05, 01);

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement: null);

            Assert.Equal(echeanceLegale, row.EcheanceLegale);
            Assert.Null(row.EcartJours);
        }

        [Fact]
        public void FactureNonSoldee_SoldeRestant_EcartCalculeContreAujourdhui_Provisoire()
        {
            // Réglé = 0 (rien payé) ⇒ solde restant = TTC > 0 ⇒ écart mesuré contre la date du jour.
            var row = NouvelleLigne(montantTtc: 1000m, regle: 0m);
            var echeanceLegale = DateTime.Today.AddDays(-30);

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement: null);

            Assert.Equal(echeanceLegale, row.EcheanceLegale);
            Assert.Equal(30, row.EcartJours); // toujours calculable, même sans rapprochement.
        }

        [Fact]
        public void FacturePartielle_SoldeRestantPositif_TraiteeCommeNonSoldee()
        {
            // Réglé partiel (500 sur 1000) ⇒ SoldeFacture = 500 > 0 ⇒ écart contre aujourd'hui,
            // même si une date de rapprochement partielle est fournie (ignorée : provisoire).
            var row = NouvelleLigne(montantTtc: 1000m, regle: 500m);
            var echeanceLegale = DateTime.Today.AddDays(-10);

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement: new DateTime(2020, 01, 01));

            Assert.Equal(10, row.EcartJours);
        }

        [Fact]
        public void FactureSoldee_MemeJourQueEcheance_EcartZero()
        {
            var row = NouvelleLigne(montantTtc: 1000m, regle: 1000m);
            var echeanceLegale = new DateTime(2026, 06, 10);

            row.AppliquerDelaiPaiement(echeanceLegale, derniereDateRapprochement: new DateTime(2026, 06, 10));

            Assert.Equal(0, row.EcartJours);
        }
    }
}
