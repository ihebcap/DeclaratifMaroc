using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-131 — Cœur métier DDP : sélection des lignes hors délai + calcul INCRÉMENTAL
    /// anti-double-déclaration. Tests PURS, hors base (mêmes standards que
    /// <see cref="EcheanceLegaleCalculatorTests"/> / <see cref="DelaiPaiementBootstrapGuardTests"/>).
    ///
    /// Couvre les 3 anomalies legacy corrigées (décision PO 19/07/2026) + le garde-fou de bascule
    /// TASK-128 :
    /// <list type="number">
    /// <item>anti-double-déclaration (scénario T1/T2 du PO : 60 jours puis 15, jamais 75) ;</item>
    /// <item>affectation partielle scindée part affectée / part non affectée ;</item>
    /// <item>Depassement du cas 1 non plus constant (= longueur de période) mais incrémental ;</item>
    /// <item>garde-fou de mise en route, avec et sans reprise manuelle.</item>
    /// </list>
    /// </summary>
    public class SelectionDelaiPaiementCalculatorTests
    {
        // Société basculée depuis longtemps : le garde-fou TASK-128 laisse passer le calcul
        // automatique pour toutes les échéances des scénarios (sauf tests dédiés au garde-fou).
        private static readonly DateTime MiseEnRouteAncienne = new DateTime(2023, 1, 1);

        // ── Fabriques ─────────────────────────────────────────────────────────────────────────

        private static EcheanceDelaiPaiement Echeance(
            int ecId,
            DateTime doDate,
            decimal montant = 50_000m,
            decimal solde = 50_000m,
            EtatEcheanceDelaiPaiement etat = EtatEcheanceDelaiPaiement.NonPaye)
            => new EcheanceDelaiPaiement
            {
                EcId = ecId,
                EcNo = ecId,
                DoNumero = $"FA{ecId}",
                DoDate = doDate,
                Etat = etat,
                Montant = montant,
                Solde = solde,
                MontantDevise = montant,
                SoldeDevise = solde,
                EcheanceContractuelle = doDate.AddDays(30),
                TiersNo = 100,
                TiersCode = "FR001",
                TiersIntitule = "Fournisseur test"
            };

        private static AffectationDelaiPaiement AffectationCheque(
            int afId,
            int ecId,
            decimal montant,
            DateTime dateReglement,
            DateTime? dateRapprochement,
            bool comptabilise = true)
            => new AffectationDelaiPaiement
            {
                AfId = afId,
                EcId = ecId,
                AfDate = dateReglement,
                Montant = montant,
                MontantDevise = montant,
                MvId = 9_000 + afId,
                TypeReglement = TypeReglementDelaiPaiement.Cheque,
                DateReglement = dateReglement,
                EstRapproche = dateRapprochement.HasValue,
                DateRapprochement = dateRapprochement,
                EstComptabilise = comptabilise,
                ReglementNumero = $"REG{afId}",
                ReglementPiece = $"CHQ{afId}"
            };

        private static AffectationDelaiPaiement AffectationEspece(
            int afId,
            int ecId,
            decimal montant,
            DateTime dateReglement,
            bool comptabilise = true)
            => new AffectationDelaiPaiement
            {
                AfId = afId,
                EcId = ecId,
                AfDate = dateReglement,
                Montant = montant,
                MontantDevise = montant,
                MvId = 9_000 + afId,
                TypeReglement = TypeReglementDelaiPaiement.Espece,
                DateReglement = dateReglement,
                EstRapproche = false,
                DateRapprochement = null,
                EstComptabilise = comptabilise,
                ReglementNumero = $"REG{afId}",
                ReglementPiece = null
            };

        /// <summary>Échéance légale imposée directement (la résolution TASK-127 est testée à part).</summary>
        private static Dictionary<int, ResultatDelaiPaiement> Legale(params (int EcId, DateTime EcheanceLegale)[] entrees)
            => entrees.ToDictionary(
                e => e.EcId,
                e => new ResultatDelaiPaiement
                {
                    EcheanceLegale = e.EcheanceLegale,
                    NombreJoursApplique = 60,
                    OrigineDelai = OrigineDelai.Defaut
                });

        private static ParametresSelectionDelaiPaiement Parametres(
            DateTime dateDebut,
            DateTime dateFin,
            IEnumerable<EcheanceDelaiPaiement> echeances,
            IReadOnlyDictionary<int, ResultatDelaiPaiement> legales,
            IEnumerable<AffectationDelaiPaiement>? affectations = null,
            IReadOnlyDictionary<int, DateTime>? bornesDeclarees = null,
            DateTime? miseEnRoute = null,
            IReadOnlyDictionary<int, DateTime>? reprises = null)
            => new ParametresSelectionDelaiPaiement
            {
                DateDebutPeriode = dateDebut,
                DateFinPeriode = dateFin,
                Echeances = echeances.ToList(),
                Affectations = (affectations ?? Enumerable.Empty<AffectationDelaiPaiement>()).ToList(),
                EcheancesLegales = legales,
                DernieresBornesDeclarees = bornesDeclarees ?? new Dictionary<int, DateTime>(),
                DateMiseEnRouteSociete = miseEnRoute ?? MiseEnRouteAncienne,
                ReprisesManuelles = reprises ?? new Dictionary<int, DateTime>()
            };

        // ══ ANOMALIE 1 — anti-double-déclaration (scénario T1/T2 validé par le PO) ══════════════

        [Fact]
        public void AnomalieUn_T1PuisT2_NeRedeclareJamaisLeRetardDejaDeclare()
        {
            // Facture dont l'échéance légale tombe le 2025-01-31.
            // T1 = 2025-01-01..2025-04-01 : non payée => retard déclaré = 60 j (31/01 -> 01/04).
            // T2 = 2025-04-02..2025-07-01 : réellement payée (chèque rapproché) le 2025-04-16,
            //      soit 75 j de retard réel. Attendu : 15 (75 - 60), JAMAIS 75.
            var echeanceLegale = new DateTime(2025, 1, 31);
            var ec = Echeance(1, new DateTime(2024, 12, 1));
            var legales = Legale((1, echeanceLegale));

            // ── T1 : aucune déclaration antérieure, échéance non payée, échéance légale hors période
            //    (< dateDebut de T1 ? non : 31/01 est DANS T1) => cas 3, solde non affecté.
            var t1 = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 4, 1),
                new[] { ec }, legales));

            var ligneT1 = Assert.Single(t1);
            Assert.Equal(60, ligneT1.Depassement);
            Assert.Equal(OrigineBorneReference.EcheanceLegale, ligneT1.OrigineBorneReference);
            Assert.Equal(echeanceLegale, ligneT1.BorneReference);

            // ── T2 : la ligne T1 a été intégrée dans une déclaration dont DDP_DateFin = 2025-04-01.
            var ecPayee = Echeance(1, new DateTime(2024, 12, 1), solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);
            var affectation = AffectationCheque(10, 1, 50_000m,
                dateReglement: new DateTime(2025, 4, 10),
                dateRapprochement: new DateTime(2025, 4, 16));

            var t2 = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 4, 2), new DateTime(2025, 7, 1),
                new[] { ecPayee }, legales,
                affectations: new[] { affectation },
                bornesDeclarees: new Dictionary<int, DateTime> { [1] = new DateTime(2025, 4, 1) }));

            var ligneT2 = Assert.Single(t2);
            Assert.Equal(15, ligneT2.Depassement);
            Assert.Equal(OrigineBorneReference.DerniereDeclaration, ligneT2.OrigineBorneReference);
            Assert.Equal(new DateTime(2025, 4, 1), ligneT2.BorneReference);
            Assert.Equal(new DateTime(2025, 4, 16), ligneT2.BorneActuelle);
            Assert.Equal(BucketDelaiPaiement.HorsPeriodePartAffectee, ligneT2.Bucket);

            // Critère de validation TASK-131 : le cumul déclaré n'excède JAMAIS le retard réel total
            // (75 j entre l'échéance légale et le rapprochement effectif).
            Assert.Equal(75, ligneT1.Depassement!.Value + ligneT2.Depassement!.Value);
            Assert.Equal(75, (int)(new DateTime(2025, 4, 16) - echeanceLegale).TotalDays);
        }

        [Fact]
        public void AnomalieUn_LegacyAuraitDeclare75EnT2_LeCalculIncrementalDeclare15()
        {
            // Non-régression explicite : le calcul legacy du cas 2
            // ((min(datePointage, dateFin) - dateDebut)) et celui du cas 3 (date - echeanceLegale)
            // ignoraient totalement l'historique. On vérifie ici que la borne de référence utilisée
            // est bien l'historique et NON l'échéance légale.
            var echeanceLegale = new DateTime(2025, 1, 31);
            var ec = Echeance(1, new DateTime(2024, 12, 1), solde: 0m, etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 4, 2), new DateTime(2025, 7, 1),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationCheque(10, 1, 50_000m, new DateTime(2025, 4, 10), new DateTime(2025, 4, 16)) },
                bornesDeclarees: new Dictionary<int, DateTime> { [1] = new DateTime(2025, 4, 1) }));

            var ligne = Assert.Single(lignes);
            Assert.NotEqual(75, ligne.Depassement);
            Assert.Equal(15, ligne.Depassement);
        }

        [Fact]
        public void AnomalieUn_RienDeNouveauDepuisLaDerniereDeclaration_LigneExclue()
        {
            // Borne de référence (dernière déclaration) POSTÉRIEURE ou ÉGALE à la borne actuelle :
            // la ligne doit être EXCLUE, jamais proposée avec un Depassement nul ou négatif.
            var echeanceLegale = new DateTime(2025, 1, 31);
            var ec = Echeance(1, new DateTime(2024, 12, 1));

            // Déclaration antérieure déjà bornée au 2025-07-01, période courante s'arrêtant le même jour.
            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 4, 2), new DateTime(2025, 7, 1),
                new[] { ec }, Legale((1, echeanceLegale)),
                bornesDeclarees: new Dictionary<int, DateTime> { [1] = new DateTime(2025, 7, 1) }));

            Assert.Empty(lignes);
        }

        [Fact]
        public void AnomalieUn_BorneDeclareePosterieureALaPeriode_LigneExclue()
        {
            var echeanceLegale = new DateTime(2025, 1, 31);
            var ec = Echeance(1, new DateTime(2024, 12, 1));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 4, 2), new DateTime(2025, 7, 1),
                new[] { ec }, Legale((1, echeanceLegale)),
                bornesDeclarees: new Dictionary<int, DateTime> { [1] = new DateTime(2025, 12, 31) }));

            Assert.Empty(lignes);
        }

        // ══ ANOMALIE 2 — affectation partielle scindée ══════════════════════════════════════════

        [Fact]
        public void AnomalieDeux_EcheanceHorsPeriodePartiellementPayee_ScindeePartAffecteeEtPartNonAffectee()
        {
            // Échéance légale 2024-12-15 (hors période), facture de 100 000 :
            //  - 40 000 payés par chèque rapproché le 2025-02-10 (DANS la période) => cas 2, borne = 10/02
            //  - 60 000 restants non affectés => cas 1, borne = fin de période (31/03)
            var echeanceLegale = new DateTime(2024, 12, 15);
            var ec = Echeance(1, new DateTime(2024, 10, 15), montant: 100_000m, solde: 60_000m,
                etat: EtatEcheanceDelaiPaiement.NonPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationCheque(10, 1, 40_000m, new DateTime(2025, 2, 5), new DateTime(2025, 2, 10)) }));

            Assert.Equal(2, lignes.Count);

            var partAffectee = lignes.Single(l => l.Bucket == BucketDelaiPaiement.HorsPeriodePartAffectee);
            Assert.Equal(10, partAffectee.AfId);
            Assert.Equal(40_000m, partAffectee.MontantLigne);
            Assert.Equal(new DateTime(2025, 2, 10), partAffectee.BorneActuelle);
            Assert.Equal((int)(new DateTime(2025, 2, 10) - echeanceLegale).TotalDays, partAffectee.Depassement);

            var partNonAffectee = lignes.Single(l => l.Bucket == BucketDelaiPaiement.HorsPeriodePartNonAffectee);
            Assert.Null(partNonAffectee.AfId);
            Assert.Equal(60_000m, partNonAffectee.MontantLigne);   // solde restant UNIQUEMENT
            Assert.Equal(new DateTime(2025, 3, 31), partNonAffectee.BorneActuelle);
            Assert.Equal((int)(new DateTime(2025, 3, 31) - echeanceLegale).TotalDays, partNonAffectee.Depassement);
        }

        [Fact]
        public void AnomalieDeux_AffectationRapprocheeAvantLaPeriode_NeProduitPasDeLigne()
        {
            // Une affectation déjà rapprochée AVANT le début de période n'appartient pas à cette
            // période : seule la part non affectée restante doit produire une ligne (le legacy
            // émettait une ligne par affectation, filtrée ensuite par un Depassement négatif).
            var echeanceLegale = new DateTime(2024, 12, 15);
            var ec = Echeance(1, new DateTime(2024, 10, 15), montant: 100_000m, solde: 60_000m);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationCheque(10, 1, 40_000m, new DateTime(2024, 12, 20), new DateTime(2024, 12, 22)) }));

            var ligne = Assert.Single(lignes);
            Assert.Equal(BucketDelaiPaiement.HorsPeriodePartNonAffectee, ligne.Bucket);
        }

        [Fact]
        public void AnomalieDeux_ChequeAffecteMaisNonRapproche_BorneEgaleFinDePeriode()
        {
            // Le retard court toujours tant que la pièce n'est pas rapprochée : borne = fin de période.
            // (Précision documentée vs legacy, qui lisait un MV_PointDate résiduel.)
            var echeanceLegale = new DateTime(2024, 12, 15);
            var ec = Echeance(1, new DateTime(2024, 10, 15), montant: 100_000m, solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationCheque(10, 1, 100_000m, new DateTime(2025, 2, 5), dateRapprochement: null) }));

            var ligne = Assert.Single(lignes);
            Assert.Equal(new DateTime(2025, 3, 31), ligne.BorneActuelle);
            Assert.Null(ligne.DateRapprochement);
        }

        [Fact]
        public void AnomalieDeux_ChequeRapprocheApresLaPeriode_BornePlafonneeALaFinDePeriode()
        {
            var echeanceLegale = new DateTime(2024, 12, 15);
            var ec = Echeance(1, new DateTime(2024, 10, 15), montant: 100_000m, solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationCheque(10, 1, 100_000m, new DateTime(2025, 2, 5), new DateTime(2025, 6, 30)) }));

            var ligne = Assert.Single(lignes);
            Assert.Equal(new DateTime(2025, 3, 31), ligne.BorneActuelle);
        }

        // ══ ANOMALIE 3 — Depassement du cas 1 plus jamais constant ═════════════════════════════

        [Fact]
        public void AnomalieTrois_DeuxFacturesDAnciennetesDifferentes_ProduisentDesDepassementsDifferents()
        {
            // Le legacy produisait pour ces DEUX factures le MÊME Depassement (= longueur de période,
            // ici 90 j), indépendamment de leur ancienneté réelle. Avec le calcul incrémental et
            // aucune déclaration antérieure, chacune part de SA propre échéance légale.
            var ancienne = Echeance(1, new DateTime(2024, 1, 10));
            var recente = Echeance(2, new DateTime(2024, 10, 10));
            var legales = Legale(
                (1, new DateTime(2024, 3, 10)),
                (2, new DateTime(2024, 12, 10)));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ancienne, recente }, legales));

            var ligneAncienne = lignes.Single(l => l.EcId == 1);
            var ligneRecente = lignes.Single(l => l.EcId == 2);

            Assert.Equal((int)(new DateTime(2025, 3, 31) - new DateTime(2024, 3, 10)).TotalDays, ligneAncienne.Depassement);
            Assert.Equal((int)(new DateTime(2025, 3, 31) - new DateTime(2024, 12, 10)).TotalDays, ligneRecente.Depassement);
            Assert.NotEqual(ligneAncienne.Depassement, ligneRecente.Depassement);

            // Et surtout : plus jamais la longueur de la période (90 j) pour l'une comme pour l'autre.
            var longueurPeriode = (int)(new DateTime(2025, 3, 31) - new DateTime(2025, 1, 1)).TotalDays;
            Assert.NotEqual(longueurPeriode, ligneAncienne.Depassement);
            Assert.NotEqual(longueurPeriode, ligneRecente.Depassement);
        }

        // ══ GARDE-FOU DE MISE EN ROUTE (TASK-128) ══════════════════════════════════════════════

        [Fact]
        public void GardeFou_EcheanceAnterieureALaMiseEnRoute_SansReprise_LigneSignaleeSansChiffre()
        {
            var echeanceLegale = new DateTime(2024, 6, 30);
            var ec = Echeance(1, new DateTime(2024, 4, 30));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                miseEnRoute: new DateTime(2025, 1, 1)));

            var ligne = Assert.Single(lignes);
            Assert.Equal(StatutLigneDelaiPaiement.RepriseManuelleRequise, ligne.Statut);
            Assert.Null(ligne.Depassement);          // JAMAIS un chiffre silencieusement faux
            Assert.Null(ligne.BorneReference);
            Assert.Equal(OrigineBorneReference.Indeterminee, ligne.OrigineBorneReference);
        }

        [Fact]
        public void GardeFou_EcheanceAnterieureALaMiseEnRoute_AvecReprise_CalculDepuisLaBorneDeReprise()
        {
            var echeanceLegale = new DateTime(2024, 6, 30);
            var ec = Echeance(1, new DateTime(2024, 4, 30));
            var borneReprise = new DateTime(2024, 12, 31);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                miseEnRoute: new DateTime(2025, 1, 1),
                reprises: new Dictionary<int, DateTime> { [1] = borneReprise }));

            var ligne = Assert.Single(lignes);
            Assert.Equal(StatutLigneDelaiPaiement.Candidate, ligne.Statut);
            Assert.Equal(OrigineBorneReference.RepriseManuelle, ligne.OrigineBorneReference);
            Assert.Equal(borneReprise, ligne.BorneReference);
            Assert.Equal((int)(new DateTime(2025, 3, 31) - borneReprise).TotalDays, ligne.Depassement);
        }

        [Fact]
        public void GardeFou_EcheanceAnterieureMaisDejaHistorisee_CalculAutomatiqueDepuisLHistorique()
        {
            // Historique présent (déclaration passée, y compris via l'ancien applicatif) : la borne de
            // référence est l'historique, la reprise manuelle n'est pas requise.
            var echeanceLegale = new DateTime(2024, 6, 30);
            var ec = Echeance(1, new DateTime(2024, 4, 30));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                bornesDeclarees: new Dictionary<int, DateTime> { [1] = new DateTime(2024, 12, 31) },
                miseEnRoute: new DateTime(2025, 1, 1)));

            var ligne = Assert.Single(lignes);
            Assert.Equal(StatutLigneDelaiPaiement.Candidate, ligne.Statut);
            Assert.Equal(OrigineBorneReference.DerniereDeclaration, ligne.OrigineBorneReference);
            Assert.Equal(90, ligne.Depassement);   // 31/12/2024 -> 31/03/2025
        }

        [Fact]
        public void GardeFou_SocieteSansDateDeMiseEnRoute_AucuneLigneChiffree()
        {
            // Décision TASK-128 (documentée) : société non configurée => calcul automatique désactivé
            // INCONDITIONNELLEMENT, même avec un historique. Les lignes restent visibles mais sans chiffre.
            var ec = Echeance(1, new DateTime(2024, 10, 10));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(new ParametresSelectionDelaiPaiement
            {
                DateDebutPeriode = new DateTime(2025, 1, 1),
                DateFinPeriode = new DateTime(2025, 3, 31),
                Echeances = new[] { ec },
                EcheancesLegales = Legale((1, new DateTime(2024, 12, 10))),
                DernieresBornesDeclarees = new Dictionary<int, DateTime> { [1] = new DateTime(2024, 12, 31) },
                DateMiseEnRouteSociete = null
            });

            var ligne = Assert.Single(lignes);
            Assert.Equal(StatutLigneDelaiPaiement.RepriseManuelleRequise, ligne.Statut);
            Assert.Null(ligne.Depassement);
        }

        [Fact]
        public void GardeFou_LigneSansAucunRetardReel_NonSignaleeCommeRepriseRequise()
        {
            // Garde-fou actif, MAIS la borne actuelle n'atteint pas l'échéance légale : il n'y a
            // aucun retard, quelle que soit la borne de référence. Inutile de polluer l'écran de
            // contrôle avec une « reprise manuelle requise » qui n'aurait rien produit.
            var echeanceLegale = new DateTime(2025, 3, 20);
            var ec = Echeance(1, new DateTime(2025, 1, 19), solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationEspece(10, 1, 50_000m, new DateTime(2025, 3, 10), comptabilise: false) },
                miseEnRoute: new DateTime(2026, 1, 1)));   // échéance très antérieure => garde-fou actif

            Assert.Empty(lignes);
        }

        [Fact]
        public void GardeFou_LigneAvecRetardReel_ResteSignaleeCommeRepriseRequise()
        {
            // Contrôle du garde-fou précédent : dès qu'un retard existe (borne actuelle > échéance
            // légale), la ligne DOIT rester visible — jamais de suppression silencieuse.
            var echeanceLegale = new DateTime(2025, 2, 10);
            var ec = Echeance(1, new DateTime(2024, 12, 12));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                miseEnRoute: new DateTime(2026, 1, 1)));

            var ligne = Assert.Single(lignes);
            Assert.Equal(StatutLigneDelaiPaiement.RepriseManuelleRequise, ligne.Statut);
            Assert.Null(ligne.Depassement);
        }

        // ══ CAS 3 (échéance légale DANS la période) ════════════════════════════════════════════

        [Fact]
        public void CasTrois_EspecePayeeApresLEcheanceLegale_DepassementJusquALaDateDeReglement()
        {
            var echeanceLegale = new DateTime(2025, 2, 10);
            var ec = Echeance(1, new DateTime(2024, 12, 12), solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationEspece(10, 1, 50_000m, new DateTime(2025, 2, 25)) }));

            var ligne = Assert.Single(lignes);
            Assert.Equal(BucketDelaiPaiement.DansPeriodePartAffectee, ligne.Bucket);
            Assert.Equal(new DateTime(2025, 2, 25), ligne.BorneActuelle);
            Assert.Equal(15, ligne.Depassement);
        }

        [Fact]
        public void CasTrois_EspecePayeeAvantLEcheanceLegale_AucuneLigne()
        {
            // Réglée dans les délais et comptabilisée : la condition d'inclusion legacy est fausse.
            var echeanceLegale = new DateTime(2025, 2, 10);
            var ec = Echeance(1, new DateTime(2024, 12, 12), solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationEspece(10, 1, 50_000m, new DateTime(2025, 2, 1)) }));

            Assert.Empty(lignes);
        }

        [Fact]
        public void CasTrois_ReglementNonComptabiliseDansLesDelais_InclusParLegacyMaisDepassementNul_DoncExclu()
        {
            // Le legacy inclut TOUT règlement non comptabilisé ; le filtre final Depassement > 0
            // l'élimine si le règlement est dans les délais. Comportement reproduit à l'identique.
            var echeanceLegale = new DateTime(2025, 2, 10);
            var ec = Echeance(1, new DateTime(2024, 12, 12), solde: 0m,
                etat: EtatEcheanceDelaiPaiement.TotalementPaye);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale)),
                affectations: new[] { AffectationEspece(10, 1, 50_000m, new DateTime(2025, 2, 1), comptabilise: false) }));

            Assert.Empty(lignes);
        }

        [Fact]
        public void CasTrois_SoldeRestantNonAffecte_ProduitUneLigneJusquALaFinDePeriode()
        {
            var echeanceLegale = new DateTime(2025, 2, 10);
            var ec = Echeance(1, new DateTime(2024, 12, 12), montant: 100_000m, solde: 100_000m);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, echeanceLegale))));

            var ligne = Assert.Single(lignes);
            Assert.Equal(BucketDelaiPaiement.DansPeriodePartNonAffectee, ligne.Bucket);
            Assert.Null(ligne.AfId);
            Assert.Equal(new DateTime(2025, 3, 31), ligne.BorneActuelle);
            Assert.Equal(49, ligne.Depassement);   // 10/02 -> 31/03
        }

        [Fact]
        public void EcheanceLegalePosterieureALaPeriode_AucuneLigne()
        {
            var ec = Echeance(1, new DateTime(2025, 3, 20));

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, new DateTime(2025, 5, 19)))));

            Assert.Empty(lignes);
        }

        // ══ SEUILS LÉGAUX (reproduits à l'identique du legacy) ═════════════════════════════════

        [Fact]
        public void SeuilsLegaux_FactureAnterieureAu1erJuillet2023_JamaisEligible()
        {
            Assert.False(SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(new DateTime(2023, 6, 30), 1_000_000m));
            Assert.True(SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(new DateTime(2023, 7, 1), 10_000m));
        }

        [Fact]
        public void SeuilsLegaux_SeuilDeMontantApplicableUniquementJusquAu31Decembre2024()
        {
            // Avant/le 31/12/2024 : seuil 10 000 applicable.
            Assert.False(SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(new DateTime(2024, 12, 31), 9_999.99m));
            Assert.True(SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(new DateTime(2024, 12, 31), 10_000m));
            // Après le 31/12/2024 : plus de seuil de montant.
            Assert.True(SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(new DateTime(2025, 1, 1), 1m));
        }

        [Fact]
        public void SeuilsLegaux_AppliquesParLeCalculateur_EcheanceSousSeuilExclue()
        {
            var ec = Echeance(1, new DateTime(2024, 6, 30), montant: 5_000m, solde: 5_000m);

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                new[] { ec }, Legale((1, new DateTime(2024, 8, 29)))));

            Assert.Empty(lignes);
        }

        // ══ GARDES / COHÉRENCE ════════════════════════════════════════════════════════════════

        [Fact]
        public void EcheanceSansEcheanceLegaleResolue_LeveUneErreurExplicite()
        {
            // Aucune échéance ne doit être ignorée silencieusement.
            var ec = Echeance(1, new DateTime(2025, 1, 10));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                    new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                    new[] { ec }, new Dictionary<int, ResultatDelaiPaiement>())));

            Assert.Contains("EC_Id=1", ex.Message);
        }

        [Fact]
        public void PeriodeInvalide_LeveUneErreurExplicite()
        {
            Assert.Throws<ArgumentException>(() =>
                SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                    new DateTime(2025, 3, 31), new DateTime(2025, 1, 1),
                    Array.Empty<EcheanceDelaiPaiement>(), new Dictionary<int, ResultatDelaiPaiement>())));
        }

        [Fact]
        public void ToutesLesLignesRetenuesOntUnDepassementStrictementPositifOuAucunChiffre()
        {
            // Invariant global du livrable (critère TASK-131 : « Depassement > 0 uniquement »).
            var echeances = new[]
            {
                Echeance(1, new DateTime(2024, 1, 10)),
                Echeance(2, new DateTime(2024, 10, 10), montant: 100_000m, solde: 40_000m),
                Echeance(3, new DateTime(2025, 1, 5), solde: 0m, etat: EtatEcheanceDelaiPaiement.TotalementPaye)
            };
            var legales = Legale(
                (1, new DateTime(2024, 3, 10)),
                (2, new DateTime(2024, 12, 10)),
                (3, new DateTime(2025, 3, 6)));
            var affectations = new[]
            {
                AffectationCheque(10, 2, 60_000m, new DateTime(2025, 2, 1), new DateTime(2025, 2, 6)),
                AffectationEspece(11, 3, 50_000m, new DateTime(2025, 3, 20))
            };

            var lignes = SelectionDelaiPaiementCalculator.Selectionner(Parametres(
                new DateTime(2025, 1, 1), new DateTime(2025, 3, 31),
                echeances, legales, affectations: affectations));

            Assert.NotEmpty(lignes);
            Assert.All(lignes, l =>
            {
                if (l.Statut == StatutLigneDelaiPaiement.Candidate)
                {
                    Assert.NotNull(l.Depassement);
                    Assert.True(l.Depassement > 0, $"Depassement non strictement positif sur EC_Id={l.EcId}/AF_Id={l.AfId}.");
                    Assert.NotNull(l.BorneReference);
                }
                else
                {
                    Assert.Null(l.Depassement);
                    Assert.Null(l.BorneReference);
                }
            });
        }
    }
}
