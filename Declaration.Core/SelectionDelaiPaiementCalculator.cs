using System;
using System.Collections.Generic;
using System.Linq;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-131 — Seuils légaux de la Déclaration Délai de Paiement Maroc. Dates statutaires de la loi
    /// marocaine : AUCUN paramétrage par client (reproduction à l'identique du legacy
    /// <c>LigneControleDelaisPaiementController.GetAll</c>, l.71-76). Point UNIQUE de vérité : la
    /// requête de sélection (repository) reçoit ces valeurs en paramètres depuis ici, et le
    /// calculateur pur les réapplique défensivement — jamais deux littéraux indépendants.
    /// </summary>
    public static class SeuilsLegauxDelaiPaiement
    {
        /// <summary>Début de la déclaration légale (legacy <c>dateDebDecLoi</c>) : aucune facture antérieure n'est déclarable.</summary>
        public static readonly DateTime DateDebutDeclarationLoi = new DateTime(2023, 7, 1);

        /// <summary>Date au-delà de laquelle le seuil de montant ne s'applique plus (legacy <c>dateLimiteMontant</c>).</summary>
        public static readonly DateTime DateLimiteSeuilMontant = new DateTime(2024, 12, 31);

        /// <summary>Seuil de montant (devise société) applicable UNIQUEMENT jusqu'à <see cref="DateLimiteSeuilMontant"/> inclus.</summary>
        public const decimal SeuilMontant = 10_000m;

        /// <summary>
        /// Éligibilité "seuils légaux" d'une facture : date document ≥ 2023-07-01 ET (date document
        /// &gt; 2024-12-31 OU montant ≥ 10 000). Le montant est celui de la facture EN DEVISE SOCIÉTÉ
        /// (le filtre devise société est appliqué en amont, comme dans le legacy).
        /// </summary>
        public static bool EstEligibleSeuilLegal(DateTime dateDocument, decimal montant)
            => dateDocument.Date >= DateDebutDeclarationLoi
               && (dateDocument.Date > DateLimiteSeuilMontant || montant >= SeuilMontant);
    }

    /// <summary>
    /// TASK-131 : type de règlement (<c>RT_MOUVEMENT.MV_Type</c>) — valeurs reprises telles quelles de
    /// l'enum legacy <c>ReglementType</c> (<c>Tresorerie.Core/Enum/ReglementType.cs</c>), colonne mappée
    /// par <c>MouvementMapping.cs:32</c>. Chèque/Traite/Virement = « pièce » (date déterminante = date
    /// de pointage) ; Espèce/Autre = date déterminante sur la date de règlement.
    /// </summary>
    public enum TypeReglementDelaiPaiement
    {
        Espece = 0,
        Cheque = 1,
        Traite = 2,
        Virement = 3,
        Autre = 4
    }

    /// <summary>
    /// TASK-131 : état d'une échéance (<c>RT_ECHEANCE.EC_Etat</c>) — enum legacy <c>Etat</c>
    /// (0 = NonPaye, 1 = TotalementPaye).
    /// </summary>
    public enum EtatEcheanceDelaiPaiement
    {
        NonPaye = 0,
        TotalementPaye = 1
    }

    /// <summary>
    /// TASK-131 : données d'UNE échéance candidate, projetées en LECTURE SEULE depuis
    /// <c>RT_ECHEANCE</c>. Type PUR (aucune dépendance DB) : le repository projette directement
    /// dedans (même principe que <see cref="ConventionDelaiPaiement"/>, TASK-127).
    /// </summary>
    public sealed class EcheanceDelaiPaiement
    {
        public int EcId { get; init; }
        public int EcNo { get; init; }

        /// <summary>Numéro de pièce du document (<c>DO_Numero</c>) — sert aussi à l'appariement convention Facture (TASK-127).</summary>
        public string? DoNumero { get; init; }

        /// <summary>Date du document/facture (<c>DO_Date</c>) — base du calcul de l'échéance légale.</summary>
        public DateTime DoDate { get; init; }

        /// <summary>Référence document (<c>DO_Reference</c>) — traçabilité uniquement.</summary>
        public string? DoReference { get; init; }

        /// <summary>État courant (<c>EC_Etat</c>) : 0 = NonPaye, 1 = TotalementPaye.</summary>
        public EtatEcheanceDelaiPaiement Etat { get; init; }

        /// <summary>Montant de l'échéance dans sa devise (<c>EC_Montant</c>) — devise société garantie par le filtre de sélection.</summary>
        public decimal Montant { get; init; }

        /// <summary>Solde restant courant (<c>EC_Solde</c>) = part NON affectée (base du bucket « part non affectée »).</summary>
        public decimal Solde { get; init; }

        public decimal MontantDevise { get; init; }
        public decimal SoldeDevise { get; init; }

        /// <summary>Échéance contractuelle Sage (<c>EC_Echeance</c>) — informative, JAMAIS l'échéance légale.</summary>
        public DateTime EcheanceContractuelle { get; init; }

        public int TiersNo { get; init; }
        public string? TiersCode { get; init; }
        public string? TiersIntitule { get; init; }
    }

    /// <summary>
    /// TASK-131 : une affectation (<c>RT_AFFECTATION</c>) jointe à son règlement
    /// (<c>RT_MOUVEMENT</c>), projetée en LECTURE SEULE. Type PUR.
    /// </summary>
    public sealed class AffectationDelaiPaiement
    {
        public int AfId { get; init; }
        public int EcId { get; init; }

        /// <summary>Date d'affectation (<c>AF_Date</c>) — informative : JAMAIS la date déterminante du retard.</summary>
        public DateTime AfDate { get; init; }

        /// <summary>Montant affecté (<c>AF_Montant</c>) = part payée de l'échéance couverte par cette ligne.</summary>
        public decimal Montant { get; init; }
        public decimal MontantDevise { get; init; }

        public int MvId { get; init; }
        public TypeReglementDelaiPaiement TypeReglement { get; init; }

        /// <summary>Date du règlement (<c>MV_Date</c>).</summary>
        public DateTime DateReglement { get; init; }

        /// <summary>Rapprochement bancaire effectif (<c>MV_Point &lt;&gt; 0</c>).</summary>
        public bool EstRapproche { get; init; }

        /// <summary>
        /// Date de rapprochement (<c>MV_PointDate</c>) — null si non rapproché. Le repository purge le
        /// sentinel SQL <c>1753-01-01</c> ; en base réelle <c>MV_PointDate</c> peut par ailleurs porter
        /// une date FUTURE alors que <c>MV_Point = 0</c> (constaté sur GR_EMA_DISTRIBUTION) : seul
        /// <see cref="EstRapproche"/> fait foi (même convention que le module TVA / TASK-135).
        /// </summary>
        public DateTime? DateRapprochement { get; init; }

        /// <summary>Règlement comptabilisé (<c>MV_Compta &lt;&gt; 0</c>, enum legacy <c>EtatComptabilite</c>).</summary>
        public bool EstComptabilise { get; init; }

        public string? ReglementNumero { get; init; }
        public string? ReglementPiece { get; init; }

        /// <summary>Chèque/Traite/Virement : règlement « pièce » (date déterminante = date de pointage).</summary>
        public bool EstPiece =>
            TypeReglement == TypeReglementDelaiPaiement.Cheque
            || TypeReglement == TypeReglementDelaiPaiement.Traite
            || TypeReglement == TypeReglementDelaiPaiement.Virement;

        /// <summary>
        /// Date déterminante du règlement au sens DDP : pour une pièce, la date de rapprochement
        /// (null si non rapprochée) ; pour l'espèce/autre, la date de règlement (auto-rapprochée —
        /// même convention que le module TVA, cf. <c>DeclarationRepository</c> §TASK-135).
        /// </summary>
        public DateTime? DateDeterminante =>
            EstPiece
                ? (EstRapproche ? DateRapprochement?.Date : null)
                : DateReglement.Date;
    }

    /// <summary>
    /// TASK-131 : cas de figure ayant produit la ligne (traçabilité — principe « aucune ligne
    /// silencieuse »). Les 3 cas du CDC §3.1 / legacy, le cas 1 étant scindé par la correction de
    /// l'anomalie n°2 (affectation partielle).
    /// </summary>
    public enum BucketDelaiPaiement
    {
        /// <summary>Cas 1 corrigé : échéance légale hors période, part NON affectée restante (solde).</summary>
        HorsPeriodePartNonAffectee = 1,

        /// <summary>Cas 2 : échéance légale hors période, part affectée réglée/rapprochée pendant la période.</summary>
        HorsPeriodePartAffectee = 2,

        /// <summary>Cas 3 : échéance légale DANS la période, part affectée.</summary>
        DansPeriodePartAffectee = 3,

        /// <summary>Cas 3 (suite legacy) : échéance légale DANS la période, solde restant non affecté.</summary>
        DansPeriodePartNonAffectee = 4
    }

    /// <summary>TASK-131 : statut d'exploitabilité de la ligne.</summary>
    public enum StatutLigneDelaiPaiement
    {
        /// <summary>Ligne candidate exploitable : <c>Depassement</c> incrémental calculé et strictement positif.</summary>
        Candidate
    }

    /// <summary>TASK-131 : origine de la borne de référence du calcul incrémental (traçabilité).</summary>
    public enum OrigineBorneReference
    {
        /// <summary>Max <c>DDP.DDP_DateFin</c> parmi les lignes <c>RT_DECLARATIONDELAISPAIEMENTLG</c> de cet EC_Id.</summary>
        DerniereDeclaration,

        /// <summary>Aucune déclaration antérieure : borne = échéance légale (« première déclaration »).</summary>
        EcheanceLegale
    }

    /// <summary>
    /// TASK-131 : une ligne candidate de la Déclaration Délai de Paiement, avec son
    /// <see cref="Depassement"/> INCRÉMENTAL et toute la traçabilité du calcul (bornes + origines).
    /// </summary>
    public sealed class LigneSelectionDelaiPaiement
    {
        public int EcId { get; init; }

        /// <summary>Affectation d'origine (<c>AF_Id</c>), null pour les buckets « part non affectée ».</summary>
        public int? AfId { get; init; }

        public BucketDelaiPaiement Bucket { get; init; }
        public StatutLigneDelaiPaiement Statut { get; init; }

        /// <summary>Échéance légale résolue (TASK-127).</summary>
        public DateTime EcheanceLegale { get; init; }

        public int NombreJoursDelaiApplique { get; init; }
        public OrigineDelai OrigineDelai { get; init; }

        /// <summary>
        /// Borne ACTUELLE : jusqu'à quelle date le retard est constaté par cette période
        /// (fin de période, ou date de règlement/rapprochement si antérieure).
        /// </summary>
        public DateTime BorneActuelle { get; init; }

        /// <summary>Borne de RÉFÉRENCE : jusqu'à quelle date le retard a DÉJÀ été déclaré.</summary>
        public DateTime? BorneReference { get; init; }

        public OrigineBorneReference OrigineBorneReference { get; init; }

        /// <summary>
        /// Dépassement INCRÉMENTAL en jours = <see cref="BorneActuelle"/> − <see cref="BorneReference"/>,
        /// strictement positif.
        /// </summary>
        public int? Depassement { get; init; }

        /// <summary>Montant porté par CETTE ligne : montant affecté (buckets part affectée) ou solde restant (buckets part non affectée).</summary>
        public decimal MontantLigne { get; init; }

        public decimal MontantLigneDevise { get; init; }

        // ── Identification (traçabilité écran de contrôle / export) ──
        public string? DoNumero { get; init; }
        public DateTime DoDate { get; init; }
        public string? DoReference { get; init; }
        public DateTime EcheanceContractuelle { get; init; }
        public decimal MontantEcheance { get; init; }
        public decimal SoldeEcheance { get; init; }
        public int TiersNo { get; init; }
        public string? TiersCode { get; init; }
        public string? TiersIntitule { get; init; }

        public int? MvId { get; init; }
        public TypeReglementDelaiPaiement? TypeReglement { get; init; }
        public DateTime? DateReglement { get; init; }
        public DateTime? DateRapprochement { get; init; }
        public bool? ReglementComptabilise { get; init; }
        public string? ReglementNumero { get; init; }
        public string? ReglementPiece { get; init; }
    }

    /// <summary>
    /// TASK-131 : entrées (toutes déjà lues/résolues par l'appelant) du calculateur de sélection.
    /// </summary>
    public sealed class ParametresSelectionDelaiPaiement
    {
        /// <summary>Début de période de la déclaration (<c>DDP_DateBebut</c> de la déclaration EnCours).</summary>
        public DateTime DateDebutPeriode { get; init; }

        /// <summary>Fin de période de la déclaration (<c>DDP_DateFin</c>).</summary>
        public DateTime DateFinPeriode { get; init; }

        public IReadOnlyCollection<EcheanceDelaiPaiement> Echeances { get; init; } = Array.Empty<EcheanceDelaiPaiement>();

        public IReadOnlyCollection<AffectationDelaiPaiement> Affectations { get; init; } = Array.Empty<AffectationDelaiPaiement>();

        /// <summary>
        /// Échéance légale résolue (TASK-127) pour CHAQUE <c>EC_Id</c> de <see cref="Echeances"/>.
        /// Une entrée manquante est une ERREUR EXPLICITE (aucune échéance n'est ignorée silencieusement).
        /// </summary>
        public IReadOnlyDictionary<int, ResultatDelaiPaiement> EcheancesLegales { get; init; } = new Dictionary<int, ResultatDelaiPaiement>();

        /// <summary>
        /// Anti-double-déclaration : max <c>DDP.DDP_DateFin</c> par <c>EC_Id</c> parmi les lignes
        /// <c>RT_DECLARATIONDELAISPAIEMENTLG</c> (toutes déclarations confondues, y compris celles de
        /// l'ancien applicatif). EC_Id absent = jamais déclarée.
        /// </summary>
        public IReadOnlyDictionary<int, DateTime> DernieresBornesDeclarees { get; init; } = new Dictionary<int, DateTime>();

        /// <summary>
        /// Date de mise en route du module pour la société (TASK-128, critère revu par TASK-220) ;
        /// null = pas encore configurée — dans ce cas AUCUNE exclusion n'est appliquée (position la
        /// plus sûre : le calcul automatique reste actif tant que le PO n'a pas configuré la date,
        /// plutôt qu'une date arbitraire).
        /// </summary>
        public DateTime? DateMiseEnRouteSociete { get; init; }
    }

    /// <summary>
    /// TASK-131 — Cœur métier de la Déclaration Délai de Paiement Maroc : sélection des lignes hors
    /// délai + calcul INCRÉMENTAL anti-double-déclaration. Calculateur PUR (hors DB), testable comme
    /// <see cref="EcheanceLegaleCalculator"/>.
    ///
    /// Porte les 3 cas de figure du legacy
    /// (<c>LigneControleDelaisPaiementController.GetAll</c>, l.60-293) AVEC les 3 corrections décidées
    /// par le PO le 19/07/2026 :
    ///
    /// <b>Correction n°1 — anti-double-déclaration (absente du legacy à tous les niveaux).</b>
    /// Le <c>Depassement</c> déclaré n'est plus l'écart depuis l'échéance légale mais l'écart entre la
    /// BORNE ACTUELLE (fin de période, ou date de règlement/rapprochement si antérieure) et la BORNE DE
    /// RÉFÉRENCE = dernière borne déjà déclarée pour cette échéance (max <c>DDP_DateFin</c>), à défaut
    /// l'échéance légale (première déclaration), à défaut la reprise manuelle (TASK-128). Si la borne de
    /// référence est ≥ à la borne actuelle (rien de nouveau), la ligne est EXCLUE (jamais un
    /// <c>Depassement</c> nul ou négatif).
    ///
    /// <b>Correction n°2 — affectation partielle dans le bucket « hors période ».</b>
    /// Le legacy portait un <c>// TODO: verifier les affectations</c> (l.110) jamais résolu : le cas 1
    /// ne regardait que <c>Etat == NonPaye</c>. Ici, une échéance hors période est SCINDÉE : chaque
    /// affectation réglée/rapprochée pendant la période produit une ligne
    /// <see cref="BucketDelaiPaiement.HorsPeriodePartAffectee"/> (montant = <c>AF_Montant</c>), et le
    /// solde restant non affecté produit UNE ligne
    /// <see cref="BucketDelaiPaiement.HorsPeriodePartNonAffectee"/> (montant = <c>EC_Solde</c>).
    ///
    /// <b>Correction n°3 — <c>Depassement</c> du cas 1 structurellement constant.</b>
    /// Le legacy calculait <c>dateFin - Max(dateDebut, echeanceLegale)</c> alors que ce bucket filtre
    /// déjà <c>echeanceLegale &lt; dateDebut</c> : le retard valait TOUJOURS la longueur de la période.
    /// Remplacé par le calcul incrémental ci-dessus.
    ///
    /// <b>Garde-fou de mise en route (TASK-128, critère revu par TASK-220).</b> Toute échéance dont la
    /// DATE DE FACTURE (<c>DoDate</c>) est antérieure à la date de mise en route de sa société est
    /// EXCLUE PURE ET SIMPLE de la sélection (aucune ligne produite, quel que soit son bucket), même
    /// si son échéance légale calculée tombe après la mise en route. Remplace l'ancien mécanisme de
    /// "reprise manuelle" (blocage + saisie du retard initial), supprimé (décision PO explicite,
    /// TASK-220 : pas de variante "gardé mais recentré").
    ///
    /// LECTURE SEULE : ce calculateur n'écrit rien et ne connaît aucune base.
    /// </summary>
    public static class SelectionDelaiPaiementCalculator
    {
        public static IReadOnlyList<LigneSelectionDelaiPaiement> Selectionner(ParametresSelectionDelaiPaiement parametres)
        {
            if (parametres == null) throw new ArgumentNullException(nameof(parametres));

            var dateDebut = parametres.DateDebutPeriode.Date;
            var dateFin = parametres.DateFinPeriode.Date;
            if (dateFin < dateDebut)
                throw new ArgumentException("Période invalide : la fin de période est antérieure au début de période.", nameof(parametres));

            var affectationsParEcheance = parametres.Affectations
                .GroupBy(a => a.EcId)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.AfId).ToList());

            var lignes = new List<LigneSelectionDelaiPaiement>();

            foreach (var echeance in parametres.Echeances.OrderBy(e => e.EcId))
            {
                // Seuils légaux réappliqués défensivement (la requête de sélection les applique déjà
                // côté SQL avec LES MÊMES constantes — cf. SeuilsLegauxDelaiPaiement).
                if (!SeuilsLegauxDelaiPaiement.EstEligibleSeuilLegal(echeance.DoDate, echeance.Montant))
                    continue;

                if (!parametres.EcheancesLegales.TryGetValue(echeance.EcId, out var resultatDelai))
                    throw new InvalidOperationException(
                        $"Échéance légale non résolue pour EC_Id={echeance.EcId} : le calcul du délai (TASK-127) doit être fourni pour chaque échéance candidate.");

                var echeanceLegale = resultatDelai.EcheanceLegale.Date;

                // Échéance légale postérieure à la période : rien n'est encore exigible (identique legacy,
                // qui ne retient que « dans la période » ou « avant la période »).
                if (echeanceLegale > dateFin) continue;

                // ── Garde-fou de mise en route (TASK-128, critère DoDate depuis TASK-220) : facture
                // antérieure à la mise en route de sa société ⇒ EXCLUE, aucune ligne produite, quel
                // que soit son bucket ni son échéance légale. Pas de date configurée ⇒ pas d'exclusion.
                if (parametres.DateMiseEnRouteSociete.HasValue
                    && echeance.DoDate.Date < parametres.DateMiseEnRouteSociete.Value.Date)
                {
                    continue;
                }

                // ── Borne de référence (par échéance) : anti-double-déclaration (TASK-131).
                // Historique prioritaire sur l'échéance légale (règle PO : max DDP_DateFin) ; à défaut
                // « première déclaration » = cumul depuis l'échéance légale.
                parametres.DernieresBornesDeclarees.TryGetValue(echeance.EcId, out var derniereBorneDeclaree);
                var aHistorique = parametres.DernieresBornesDeclarees.ContainsKey(echeance.EcId);
                var borneReference = aHistorique ? derniereBorneDeclaree.Date : echeanceLegale;
                var origineBorne = aHistorique ? OrigineBorneReference.DerniereDeclaration : OrigineBorneReference.EcheanceLegale;
                const StatutLigneDelaiPaiement statut = StatutLigneDelaiPaiement.Candidate;

                var affectations = affectationsParEcheance.TryGetValue(echeance.EcId, out var affs)
                    ? affs
                    : new List<AffectationDelaiPaiement>();

                if (echeanceLegale < dateDebut)
                {
                    // ── CAS 1 & 2 (échéance légale hors période) ────────────────────────────────
                    // Correction n°2 : la part AFFECTÉE réglée/rapprochée pendant la période est
                    // traitée comme le cas 2, ligne par ligne (montant = AF_Montant).
                    foreach (var affectation in affectations)
                    {
                        var borneActuelle = BorneActuelleHorsPeriode(affectation, dateDebut, dateFin);
                        if (borneActuelle == null) continue;

                        AjouterLigne(lignes, echeance, affectation, BucketDelaiPaiement.HorsPeriodePartAffectee,
                            statut, resultatDelai, borneActuelle.Value, borneReference, origineBorne);
                    }

                    // Correction n°2 (suite) + correction n°3 : la part NON affectée restante reste le
                    // cas 1, mais avec le montant du SOLDE et un Depassement incrémental (plus la
                    // longueur de période constante du legacy).
                    if (echeance.Etat == EtatEcheanceDelaiPaiement.NonPaye)
                    {
                        AjouterLigne(lignes, echeance, null, BucketDelaiPaiement.HorsPeriodePartNonAffectee,
                            statut, resultatDelai, dateFin, borneReference, origineBorne);
                    }
                }
                else
                {
                    // ── CAS 3 (échéance légale DANS la période) ────────────────────────────────
                    foreach (var affectation in affectations)
                    {
                        // Condition d'inclusion legacy reproduite à l'identique (l.206-208) : pièce non
                        // pointée ou pointée après l'échéance légale, espèce/autre réglée après
                        // l'échéance légale, OU règlement non comptabilisé.
                        var affectationInclut =
                            (affectation.EstPiece && (!affectation.EstRapproche || affectation.DateRapprochement?.Date > echeanceLegale))
                            || (!affectation.EstPiece && affectation.DateReglement.Date > echeanceLegale);

                        if (!(!affectation.EstComptabilise || affectationInclut)) continue;

                        var borneActuelle = BorneActuelleDansPeriode(affectation, dateFin);

                        AjouterLigne(lignes, echeance, affectation, BucketDelaiPaiement.DansPeriodePartAffectee,
                            statut, resultatDelai, borneActuelle, borneReference, origineBorne);
                    }

                    // Solde restant non affecté (legacy l.269-290), avec Depassement incrémental.
                    if (echeance.Etat == EtatEcheanceDelaiPaiement.NonPaye)
                    {
                        AjouterLigne(lignes, echeance, null, BucketDelaiPaiement.DansPeriodePartNonAffectee,
                            statut, resultatDelai, dateFin, borneReference, origineBorne);
                    }
                }
            }

            return lignes;
        }

        /// <summary>
        /// Borne actuelle d'une affectation d'échéance HORS période (cas 2). Retourne null quand
        /// l'affectation n'est pas concernée par cette période. Reproduit la sélection legacy
        /// (l.96-102) : <c>(regDate ∈ [dateDebut, dateFin]) || (pièce &amp;&amp; pointage &gt; dateFin)</c>,
        /// avec la précision (documentée, cf. VERIFY) qu'une pièce NON rapprochée est traitée comme
        /// « pas encore réglée » ⇒ borne = fin de période (même sémantique que le cas 3 legacy
        /// <c>!IsPointe → dateFin</c>), au lieu de dépendre d'un <c>MV_PointDate</c> résiduel.
        /// </summary>
        private static DateTime? BorneActuelleHorsPeriode(AffectationDelaiPaiement affectation, DateTime dateDebut, DateTime dateFin)
        {
            if (affectation.EstPiece)
            {
                var datePointage = affectation.EstRapproche ? affectation.DateRapprochement?.Date : null;
                if (datePointage == null) return dateFin;             // pièce affectée mais non rapprochée : le retard court toujours
                if (datePointage.Value > dateFin) return dateFin;     // rapprochée après la période : plafonné à la fin de période
                if (datePointage.Value >= dateDebut) return datePointage.Value;
                return null;                                          // rapprochée avant la période : rien à déclarer ici
            }

            var dateReglement = affectation.DateReglement.Date;
            if (dateReglement >= dateDebut && dateReglement <= dateFin) return dateReglement;
            return null;                                              // espèce/autre hors période : exclue (identique legacy)
        }

        /// <summary>
        /// Borne actuelle d'une affectation d'échéance DANS la période (cas 3). Reproduit la date
        /// déterminante legacy (l.210-218) : pièce pointée ⇒ min(pointage, fin de période) ; pièce non
        /// pointée ⇒ fin de période ; espèce/autre ⇒ min(date de règlement, fin de période).
        /// </summary>
        private static DateTime BorneActuelleDansPeriode(AffectationDelaiPaiement affectation, DateTime dateFin)
        {
            if (affectation.EstPiece)
            {
                var datePointage = affectation.EstRapproche ? affectation.DateRapprochement?.Date : null;
                if (datePointage == null) return dateFin;
                return datePointage.Value > dateFin ? dateFin : datePointage.Value;
            }

            var dateReglement = affectation.DateReglement.Date;
            return dateReglement > dateFin ? dateFin : dateReglement;
        }

        /// <summary>
        /// Construit et ajoute la ligne, en appliquant le calcul incrémental : une ligne n'est
        /// retenue que si <c>BorneActuelle − BorneReference &gt; 0</c> (jamais un <c>Depassement</c> nul
        /// ou négatif, jamais une suppression silencieuse).
        /// </summary>
        private static void AjouterLigne(
            List<LigneSelectionDelaiPaiement> lignes,
            EcheanceDelaiPaiement echeance,
            AffectationDelaiPaiement? affectation,
            BucketDelaiPaiement bucket,
            StatutLigneDelaiPaiement statut,
            ResultatDelaiPaiement resultatDelai,
            DateTime borneActuelle,
            DateTime? borneReference,
            OrigineBorneReference origineBorne)
        {
            var jours = (int)(borneActuelle - borneReference!.Value).TotalDays;
            if (jours <= 0) return;   // rien de nouveau à déclarer depuis la dernière borne
            var depassement = jours;

            var estPartAffectee = affectation != null;

            lignes.Add(new LigneSelectionDelaiPaiement
            {
                EcId = echeance.EcId,
                AfId = affectation?.AfId,
                Bucket = bucket,
                Statut = statut,
                EcheanceLegale = resultatDelai.EcheanceLegale.Date,
                NombreJoursDelaiApplique = resultatDelai.NombreJoursApplique,
                OrigineDelai = resultatDelai.OrigineDelai,
                BorneActuelle = borneActuelle,
                BorneReference = borneReference,
                OrigineBorneReference = origineBorne,
                Depassement = depassement,
                MontantLigne = estPartAffectee ? affectation!.Montant : echeance.Solde,
                MontantLigneDevise = estPartAffectee ? affectation!.MontantDevise : echeance.SoldeDevise,
                DoNumero = echeance.DoNumero,
                DoDate = echeance.DoDate.Date,
                DoReference = echeance.DoReference,
                EcheanceContractuelle = echeance.EcheanceContractuelle.Date,
                MontantEcheance = echeance.Montant,
                SoldeEcheance = echeance.Solde,
                TiersNo = echeance.TiersNo,
                TiersCode = echeance.TiersCode,
                TiersIntitule = echeance.TiersIntitule,
                MvId = affectation?.MvId,
                TypeReglement = affectation?.TypeReglement,
                DateReglement = affectation?.DateReglement.Date,
                DateRapprochement = affectation != null && affectation.EstRapproche ? affectation.DateRapprochement?.Date : null,
                ReglementComptabilise = affectation?.EstComptabilise,
                ReglementNumero = affectation?.ReglementNumero,
                ReglementPiece = affectation?.ReglementPiece
            });
        }
    }
}
