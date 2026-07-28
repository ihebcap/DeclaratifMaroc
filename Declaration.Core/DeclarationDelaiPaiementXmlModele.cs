using System;
using System.Collections.Generic;

namespace Declaration.Core.Model
{
    /// <summary>
    /// TASK-133 : type de mode de règlement, reproduit à l'identique (valeurs) le legacy
    /// <c>Tresorerie.Core.Enum.ReglementType</c> tel que persisté dans la colonne GRF EXISTANTE
    /// <c>P_MODEREGLEMENT.MR_TypeNo</c> (Espece = 0 … Autre = 4, vérifié sur données réelles
    /// <c>GR_EMA_DISTRIBUTION</c> : MR_Id=1→0, MR_Id=2→1, MR_Id=9→2, MR_Id=4→3). Type NEUF (aucune
    /// réutilisation de DLL/code WinForms) — sert uniquement à faire le pont entre <c>MR_TypeNo</c> et
    /// le code <c>modePaiement</c> DDP (DISTINCT de la table Simpl-TVA, cf.
    /// <see cref="DeclarationDelaiPaiementLigneCalculator"/>).
    /// </summary>
    public enum TypeModeReglementDelaiPaiement
    {
        Espece = 0,
        Cheque = 1,
        Traite = 2,
        Virement = 3,
        Autre = 4
    }

    /// <summary>
    /// TASK-133 : entête du fichier XML de dépôt Délai de Paiement Maroc, projection PURE (aucune
    /// dépendance base) de ce que <c>DeclarationDelaisPaiementFileGenerator.cs</c> (legacy) écrivait
    /// avant la boucle <c>listeFacturesHorsDelai</c>. Assemblé côté <c>Declaration.Application</c>
    /// (lecture <c>RT_DECLARATIONDELAISPAIEMENT</c> + <c>P_SOCIETE</c>), consommé par
    /// <c>Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter</c>.
    /// </summary>
    public sealed class EnTeteDeclarationDelaiPaiementXml
    {
        /// <summary><c>DDP_Numero</c> — sert aussi au nommage du fichier.</summary>
        public string Numero { get; init; } = string.Empty;

        /// <summary><c>DDP_Exercice</c> → tag <c>&lt;annee&gt;</c>.</summary>
        public int Exercice { get; init; }

        /// <summary>
        /// Valeur XML de <c>&lt;periode&gt;</c> : 1..4 pour un trimestre, TOUJOURS <c>5</c> pour une
        /// annuelle (décision TASK-132 §Décisions n°5 — <c>DDP_Periode</c> brut valant 0 pour une
        /// annuelle en base NE DOIT JAMAIS être émis tel quel). Résolue par
        /// <see cref="DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeXml"/>, jamais recalculée ici.
        /// </summary>
        public int PeriodeXml { get; init; }

        /// <summary>
        /// Segment de nommage de fichier : <c>"T1".."T4"</c> pour un trimestre, <c>"A"</c> pour une
        /// annuelle. Résolu par <see cref="DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier"/>.
        /// </summary>
        public string PeriodeFichier { get; init; } = string.Empty;

        /// <summary><c>P_SOCIETE.SO_Identifiant</c> (IF société, PAS <c>SO_Id</c>) → <c>&lt;identifiantFiscal&gt;</c> racine.</summary>
        public string IdentifiantFiscalSociete { get; init; } = string.Empty;

        /// <summary><c>P_SOCIETE.SO_ActiviteMarroc</c> : 1 = Normal, 2 = EntrepriseEnCourDeProcedure → <c>&lt;activite&gt;</c>.</summary>
        public int ActiviteMarrocCode { get; init; }

        /// <summary>
        /// <c>P_SOCIETE.SO_DateJugement</c> → <c>&lt;dateJugementOuvrProc&gt;</c>, émis UNIQUEMENT si
        /// <see cref="ActiviteMarrocCode"/> = 2 (legacy : bloc conditionnel entier).
        /// </summary>
        public DateTime? DateJugement { get; init; }

        /// <summary><c>P_SOCIETE.SO_ChiffreAffaire</c> → <c>&lt;chiffreAffaire&gt;</c>.</summary>
        public decimal ChiffreAffaire { get; init; }
    }

    /// <summary>
    /// TASK-133 : UNE entrée <c>&lt;FactureHorsDelai&gt;</c>, déjà entièrement calculée (aucune
    /// logique de calcul ne doit rester côté exporter — cf.
    /// <see cref="DeclarationDelaiPaiementLigneCalculator"/>).
    /// </summary>
    public sealed class FactureHorsDelaiXml
    {
        /// <summary>IF du FOURNISSEUR (pas de la société), lu sur <c>F_COMPTET</c> (base Sage).</summary>
        public string? IdentifiantFiscalFournisseur { get; init; }

        /// <summary>N° registre de commerce fournisseur (<c>F_COMPTET</c>, colonne configurable par société).</summary>
        public string? NumRc { get; init; }

        /// <summary>Adresse siège social fournisseur (<c>F_COMPTET.CT_Adresse</c>).</summary>
        public string? AdresseSiegeSocial { get; init; }

        /// <summary><c>RT_ECHEANCE.DO_Numero</c>.</summary>
        public string? NumFacture { get; init; }

        /// <summary><c>RT_ECHEANCE.DO_Date</c>.</summary>
        public DateTime DateEmission { get; init; }

        /// <summary>
        /// Dette assumée reproduite du legacy (TASK-133 §Dette assumée) : identique à
        /// <see cref="DateEmission"/>, PAS une vraie date de livraison (mapping réel jamais branché
        /// dans le legacy — non bloquant pour ce livrable, cf. TASK-133).
        /// </summary>
        public DateTime DateLivraisonMarchandise { get; init; }

        /// <summary>Échéance légale résolue (TASK-127/131) → <c>DDPL_EcheanceLegale</c>.</summary>
        public DateTime DateConvenuePaiementFacture { get; init; }

        /// <summary><c>RT_ECHEANCE.EC_Montant</c> (legacy <c>DocumentMontant</c>).</summary>
        public decimal MontantFactureTtc { get; init; }

        /// <summary>Solde restant dû calculé (<see cref="DeclarationDelaiPaiementLigneCalculator"/>).</summary>
        public decimal MontantNonEncorePaye { get; init; }

        /// <summary>Montant payé DANS la période de la déclaration (0 si non payée dans la période).</summary>
        public decimal MontantPayeHorsDelai { get; init; }

        /// <summary>
        /// <c>true</c> ⇔ le règlement est rapproché ET sa date de rapprochement tombe dans la période
        /// de la déclaration — conditionne l'émission des 3 balises optionnelles ci-dessous (legacy :
        /// bloc <c>if</c> entier).
        /// </summary>
        public bool PayeeHorsDelaiPeriode { get; init; }

        /// <summary>Émis uniquement si <see cref="PayeeHorsDelaiPeriode"/>.</summary>
        public DateTime? DatePaiementHorsDelai { get; init; }

        /// <summary>1 = Espèce, 2 = Chèque, 4 = Virement, 5 = Traite ; <c>null</c> = mode non mappé (legacy : chaîne vide).</summary>
        public int? ModePaiement { get; init; }

        /// <summary><c>RT_MOUVEMENT.MV_Piece</c>, émis uniquement si <see cref="PayeeHorsDelaiPeriode"/>.</summary>
        public string? ReferencePaiement { get; init; }
    }

    /// <summary>TASK-133 : modèle complet, prêt à sérialiser par <c>Declaration.Export.Xml.DeclarationDelaiPaiementXmlExporter</c>.</summary>
    public sealed class DeclarationDelaiPaiementXmlModele
    {
        public EnTeteDeclarationDelaiPaiementXml EnTete { get; init; } = new();
        public List<FactureHorsDelaiXml> Factures { get; init; } = new();
    }

    /// <summary>
    /// TASK-133 : calcul PUR (aucune dépendance base) d'UNE <see cref="FactureHorsDelaiXml"/> à partir
    /// des champs bruts de la ligne + de la borne de fin de la déclaration. Reproduit EXACTEMENT
    /// <c>DeclarationDelaisPaiementFileGenerator.cs:108-138</c> (legacy) :
    /// <list type="bullet">
    /// <item><c>soldeFacture</c> = solde échéance SEUL si payée dans la période, sinon solde +
    /// montant affecté (le montant affecté n'a pas encore réduit le solde tant que la période n'est
    /// pas écoulée) ;</item>
    /// <item><c>montantPaye</c> = montant affecté si payée dans la période, sinon 0 ;</item>
    /// <item>les 3 balises optionnelles (date/mode/référence de paiement) ne sortent que dans ce même
    /// cas.</item>
    /// </list>
    /// <b>Durcissement assumé</b> (documenté VERIFY) : le legacy déréférençait
    /// <c>ligne.ReglementDatePoint.Value</c> sans vérifier sa présence quand <c>ReglementIsPointe</c>
    /// était vrai (crash potentiel si incohérent) — ici, un règlement marqué rapproché SANS date de
    /// rapprochement est traité comme NON payé dans la période (jamais d'exception, jamais un
    /// <c>DateTime</c> par défaut au 01/01/0001 silencieusement écrit).
    /// </summary>
    public static class DeclarationDelaiPaiementLigneCalculator
    {
        /// <summary>Codes DDP officiels du CDC — DISTINCTS de la table Simpl-TVA (<see cref="ModePaiementLibelle"/>), jamais réutilisés entre les deux.</summary>
        public const int CodeEspece = 1;
        public const int CodeCheque = 2;
        public const int CodeVirement = 4;
        public const int CodeTraite = 5;

        public static FactureHorsDelaiXml Calculer(
            DateTime dateFinPeriode,
            string? identifiantFiscalFournisseur,
            string? numRc,
            string? adresseSiegeSocial,
            string? numFacture,
            DateTime dateEmission,
            DateTime dateConvenuePaiementFacture,
            decimal montantFactureTtc,
            decimal soldeEcheance,
            decimal? montantAffecte,
            bool? reglementRapproche,
            DateTime? dateRapprochement,
            string? referencePaiement,
            TypeModeReglementDelaiPaiement? typeModeReglement)
        {
            // Payée « hors délai » (au sens du fichier : réglée pendant la période couverte) ⇔
            // effectivement rapprochée ET une date de rapprochement exploitable tombant dans la période.
            var payeeHorsDelaiPeriode = reglementRapproche == true
                && dateRapprochement.HasValue
                && dateRapprochement.Value.Date <= dateFinPeriode.Date;

            var montantAffecteEffectif = montantAffecte ?? 0m;

            var soldeFacture = payeeHorsDelaiPeriode
                ? soldeEcheance
                : soldeEcheance + montantAffecteEffectif;

            var montantPaye = payeeHorsDelaiPeriode ? montantAffecteEffectif : 0m;

            int? modePaiement = null;
            if (payeeHorsDelaiPeriode && typeModeReglement.HasValue)
            {
                modePaiement = typeModeReglement.Value switch
                {
                    TypeModeReglementDelaiPaiement.Espece => CodeEspece,
                    TypeModeReglementDelaiPaiement.Cheque => CodeCheque,
                    TypeModeReglementDelaiPaiement.Traite => CodeTraite,
                    TypeModeReglementDelaiPaiement.Virement => CodeVirement,
                    _ => null // Autre (legacy : switch sans case → chaîne vide)
                };
            }

            return new FactureHorsDelaiXml
            {
                IdentifiantFiscalFournisseur = identifiantFiscalFournisseur,
                NumRc = numRc,
                AdresseSiegeSocial = adresseSiegeSocial,
                NumFacture = numFacture,
                DateEmission = dateEmission,
                DateLivraisonMarchandise = dateEmission, // dette assumée (cf. TASK-133 §Dette assumée)
                DateConvenuePaiementFacture = dateConvenuePaiementFacture,
                MontantFactureTtc = montantFactureTtc,
                MontantNonEncorePaye = soldeFacture,
                MontantPayeHorsDelai = montantPaye,
                PayeeHorsDelaiPeriode = payeeHorsDelaiPeriode,
                DatePaiementHorsDelai = payeeHorsDelaiPeriode ? dateRapprochement : null,
                ModePaiement = modePaiement,
                ReferencePaiement = payeeHorsDelaiPeriode ? referencePaiement : null
            };
        }
    }
}
