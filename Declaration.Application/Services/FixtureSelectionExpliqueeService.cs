using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Application.Services;

/// <summary>
/// Service de sélection sur fixtures — utilisé en environnement de test/développement
/// quand la chaîne de connexion GRF réelle n'est pas disponible.
/// Retourne un mix réaliste de lignes éligibles ET rejetées-avec-motif pour valider
/// la règle de transparence (aucun rejet silencieux).
/// </summary>
public class FixtureSelectionExpliqueeService : ISelectionExpliqueeService
{
    public Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
        int soId,
        DateTime dateDebut,
        DateTime dateFin,
        string connectionString,
        Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null)
    {
        var candidates = new List<AffectationCandidate>
        {
            // --- Lignes ELIGIBLES ---
            new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-001",
                    NumeroRapprochement = "REG-2026-001",
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 12000m,
                    DatePaiement = new DateTime(2026, 7, 3),
                    DateFacture = new DateTime(2026, 6, 28),
                    ModePaiement = "Virement",
                    Tiers = new TiersInfo { Numero = "F001", Nom = "Fournisseur Alpha", IdentifiantFiscal = "12345678", Ice = "001234567890123" }
                },
                Motif = MotifRejet.Eligible
            },
            new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-002",
                    NumeroRapprochement = "REG-2026-001",
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 7200m,
                    DatePaiement = new DateTime(2026, 7, 10),
                    DateFacture = new DateTime(2026, 7, 5),
                    ModePaiement = "Cheque",
                    Tiers = new TiersInfo { Numero = "F002", Nom = "Fournisseur Beta", IdentifiantFiscal = "87654321", Ice = "009876543210987" }
                },
                Motif = MotifRejet.Eligible
            },
            // --- Lignes REJETÉES (transparence — aucun rejet silencieux) ---
            new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-003",
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 3600m,
                    DatePaiement = new DateTime(2026, 5, 15), // Hors période juillet
                    DateFacture = new DateTime(2026, 5, 10),
                    ModePaiement = "Virement",
                    Tiers = new TiersInfo { Numero = "F003", Nom = "Fournisseur Gamma", IdentifiantFiscal = "11223344", Ice = "002233445566778" }
                },
                Motif = MotifRejet.HorsPeriode // Rejeté → visible dans GET /lignes avec MotifRejet = "Rapproché hors de la période"
            },
            new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-004",
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 4800m,
                    DatePaiement = new DateTime(2026, 7, 20),
                    DateFacture = new DateTime(2026, 7, 18),
                    ModePaiement = "Espece",
                    Tiers = new TiersInfo { Numero = "F004", Nom = "Fournisseur Delta", IdentifiantFiscal = "55667788", Ice = "003344556677889" }
                },
                Motif = MotifRejet.DejaDeclare // Rejeté → MotifRejet = "Déjà déclaré (période précédente)"
            },
            new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-005",
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 1800m,
                    DatePaiement = null,
                    DateFacture = new DateTime(2026, 7, 22),
                    ModePaiement = "Virement",
                    Tiers = new TiersInfo { Numero = "F005", Nom = "Fournisseur Epsilon", IdentifiantFiscal = "99887766", Ice = "004455667788990" }
                },
                Motif = MotifRejet.NonRapproche // Rejeté → MotifRejet = "Règlement non rapproché"
            }
        };

        return Task.FromResult<IEnumerable<AffectationCandidate>>(candidates);
    }

    /// <summary>
    /// TASK-050 — Implémentation fixture de LireFacturesDepuisPeriodeAsync.
    /// Retourne l'ensemble des candidats (toutes factures, qu'elles soient rapprochées ou non)
    /// pour simuler le comportement facture-first en environnement de test/dev sans base réelle.
    /// </summary>
    public Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
        int soId,
        DateTime dateDebut,
        DateTime dateFin,
        string connectionString)
    {
        // Fixture : retourne toutes les factures fournisseur (éligibles + non rapprochées).
        // En production, ce serait piloté par RT_ECHEANCE (toutes EC_Type=0 de la période).
        var candidatesFournisseur = new List<AffectationCandidate>
        {
            new AffectationCandidate
            {
                Affectation = new Declaration.Core.Model.AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-001",
                    NumeroRapprochement = "REG-2026-001",
                    Sens = Declaration.Core.Model.SensAffectation.Achat,
                    Source = Declaration.Core.Model.SourceAffectation.Decaissement,
                    MontantAffecte = 12000m,
                    DatePaiement = new DateTime(2026, 7, 3),
                    DateFacture = new DateTime(2026, 6, 28),
                    ModePaiement = "Virement",
                    Tiers = new Declaration.Core.Model.TiersInfo { Numero = "F001", Nom = "Fournisseur Alpha", IdentifiantFiscal = "12345678", Ice = "001234567890123" }
                },
                Motif = MotifRejet.Eligible
            },
            new AffectationCandidate
            {
                Affectation = new Declaration.Core.Model.AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-002",
                    NumeroRapprochement = "REG-2026-001",
                    Sens = Declaration.Core.Model.SensAffectation.Achat,
                    Source = Declaration.Core.Model.SourceAffectation.Decaissement,
                    MontantAffecte = 7200m,
                    DatePaiement = new DateTime(2026, 7, 10),
                    DateFacture = new DateTime(2026, 7, 5),
                    ModePaiement = "Cheque",
                    Tiers = new Declaration.Core.Model.TiersInfo { Numero = "F002", Nom = "Fournisseur Beta", IdentifiantFiscal = "87654321", Ice = "009876543210987" }
                },
                Motif = MotifRejet.Eligible
            },
            // TASK-050 : FC2600896-like — réglée par chèque (MV_Type=1, MV_DECAISSE=0),
            // auparavant écartée du surensemble, maintenant incluse via facture-first.
            new AffectationCandidate
            {
                Affectation = new Declaration.Core.Model.AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-006",
                    NumeroRapprochement = "RF26060124",
                    Sens = Declaration.Core.Model.SensAffectation.Achat,
                    Source = Declaration.Core.Model.SourceAffectation.Decaissement,
                    MontantAffecte = 8500m,
                    DatePaiement = new DateTime(2026, 6, 15),
                    DateFacture = new DateTime(2026, 6, 10),
                    ModePaiement = "Cheque",
                    Tiers = new Declaration.Core.Model.TiersInfo { Numero = "F006", Nom = "Fournisseur Zeta", IdentifiantFiscal = "24680135", Ice = "006248013579246" }
                },
                // Rapprochée non déclarée → Eligible (valorisable + déclarable)
                Motif = MotifRejet.Eligible
            },
            // Facture affectée mais non rapprochée : valorisable (affichage) mais non déclarable
            new AffectationCandidate
            {
                Affectation = new Declaration.Core.Model.AffectationADeclarer
                {
                    NumeroFacture = "FAC-2026-005",
                    NumeroRapprochement = "",
                    Sens = Declaration.Core.Model.SensAffectation.Achat,
                    Source = Declaration.Core.Model.SourceAffectation.Decaissement,
                    MontantAffecte = 1800m,
                    DatePaiement = null,
                    DateFacture = new DateTime(2026, 7, 22),
                    ModePaiement = "Virement",
                    Tiers = new Declaration.Core.Model.TiersInfo { Numero = "F005", Nom = "Fournisseur Epsilon", IdentifiantFiscal = "99887766", Ice = "004455667788990" }
                },
                Motif = MotifRejet.NonRapproche // EstValorisable=true, non déclarable
            }
        };

        return Task.FromResult<IEnumerable<AffectationCandidate>>(candidatesFournisseur);
    }
}
