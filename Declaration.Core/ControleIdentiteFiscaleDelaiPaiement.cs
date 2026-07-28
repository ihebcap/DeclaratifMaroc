using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-132 : motif précis d'invalidité de l'identité fiscale d'un fournisseur déclaré. Un motif
    /// par anomalie constatée — jamais un « invalide » global sans explication (principe « aucun
    /// blocage silencieux »).
    /// </summary>
    public enum MotifIdentiteFiscaleDelaiPaiement
    {
        /// <summary>Le fournisseur n'existe pas dans le référentiel tiers ERP (<c>F_COMPTET</c>) — legacy « Impossible de charger les informations fournisseur […] ».</summary>
        FournisseurIntrouvableDansReferentiel,

        /// <summary>Identifiant fiscal absent (NULL, vide ou uniquement des blancs).</summary>
        IdentifiantFiscalAbsent,

        /// <summary>Identifiant fiscal présent mais de longueur différente de 8.</summary>
        IdentifiantFiscalLongueurInvalide,

        /// <summary>Identifiant fiscal contenant au moins un espace.</summary>
        IdentifiantFiscalAvecEspace,

        /// <summary>ICE absent (NULL, vide ou uniquement des blancs).</summary>
        IceAbsent,

        /// <summary>ICE présent mais de longueur différente de 15.</summary>
        IceLongueurInvalide,

        /// <summary>ICE contenant au moins un espace.</summary>
        IceAvecEspace
    }

    /// <summary>
    /// TASK-132 : identité fiscale d'UN fournisseur déclaré, projetée depuis le référentiel tiers ERP
    /// (<c>F_COMPTET</c>, colonnes IF/ICE désignées par société dans <c>P_SOCIETE</c> — cf.
    /// <c>IdentiteFiscaleFournisseurConfig</c>). Type PUR : aucune dépendance base.
    /// </summary>
    public sealed class IdentiteFiscaleFournisseurDeclare
    {
        public int TiersNo { get; init; }
        public string TiersCode { get; init; } = string.Empty;
        public string? TiersIntitule { get; init; }

        /// <summary>Identifiant fiscal (IF) lu sur le maître tiers ERP.</summary>
        public string? IdentifiantFiscal { get; init; }

        /// <summary>ICE lu sur le maître tiers ERP.</summary>
        public string? Ice { get; init; }

        /// <summary>
        /// <c>false</c> quand aucune ligne du référentiel tiers ERP ne correspond à
        /// <see cref="TiersCode"/> : cas distinct d'un IF/ICE simplement vide, et lui aussi bloquant.
        /// </summary>
        public bool TrouveDansReferentiel { get; init; } = true;

        /// <summary>Nombre de lignes de la déclaration portées par ce fournisseur (traçabilité du message).</summary>
        public int NombreLignes { get; init; }
    }

    /// <summary>TASK-132 : un fournisseur fautif, avec la liste exhaustive de ses motifs.</summary>
    public sealed class FournisseurIdentiteFiscaleFautif
    {
        public int TiersNo { get; init; }
        public string TiersCode { get; init; } = string.Empty;
        public string? TiersIntitule { get; init; }
        public string? IdentifiantFiscal { get; init; }
        public string? Ice { get; init; }
        public int NombreLignes { get; init; }

        public IReadOnlyList<MotifIdentiteFiscaleDelaiPaiement> Motifs { get; init; }
            = Array.Empty<MotifIdentiteFiscaleDelaiPaiement>();

        /// <summary>Motifs rendus en français, prêts à afficher (jamais un code d'enum brut côté UI).</summary>
        public IReadOnlyList<string> MotifsLibelles { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// TASK-132 : verdict STRUCTURÉ du contrôle IF/ICE. Consommé tel quel par TASK-133 (génération
    /// XML/ZIP) qui doit l'appeler AVANT toute écriture de fichier, et par TASK-134 (affichage).
    /// </summary>
    public sealed class ResultatControleIdentiteFiscaleDelaiPaiement
    {
        /// <summary><c>true</c> ⇔ <see cref="FournisseursFautifs"/> est vide : la génération est autorisée.</summary>
        public bool EstConforme => FournisseursFautifs.Count == 0;

        /// <summary>Nombre de fournisseurs DISTINCTS examinés (traçabilité).</summary>
        public int NombreFournisseursExamines { get; init; }

        /// <summary>Nombre de lignes de déclaration examinées (traçabilité).</summary>
        public int NombreLignesExaminees { get; init; }

        public IReadOnlyList<FournisseurIdentiteFiscaleFautif> FournisseursFautifs { get; init; }
            = Array.Empty<FournisseurIdentiteFiscaleFautif>();

        /// <summary>
        /// Message bloquant explicite CITANT chaque fournisseur fautif et son motif (exigence TASK-132).
        /// Chaîne vide quand <see cref="EstConforme"/>.
        /// </summary>
        public string MessageBloquant { get; init; } = string.Empty;
    }

    /// <summary>
    /// TASK-132 — Contrôle IF/ICE fournisseur BLOQUANT avant génération du fichier de déclaration
    /// Délai de Paiement (décision PO §5.A-5, corrige l'anomalie §4.4 du CDC). Calculateur PUR,
    /// RÉUTILISABLE TEL QUEL par TASK-133 : aucun couplage à la génération, aucune écriture, aucune
    /// dépendance base — l'appelant fournit les identités déjà lues.
    ///
    /// <b>Ce que faisait le legacy et pourquoi c'était inexploitable.</b>
    /// <c>DeclarationDelaisPaiementFileGenerator.cs:78-91</c> portait ce contrôle ENTIÈREMENT EN
    /// COMMENTAIRE, et il ne pouvait pas être simplement décommenté : il référençait
    /// <c>ligne.TiersIdentifiant</c>, <c>ligne.TiersIce</c> et une variable <c>nbLigne</c> qui
    /// n'existent nulle part (le modèle <c>LigneDeclarationDelaisPaiement</c> ne porte que
    /// <c>TiersNo</c>/<c>TiersCode</c>/<c>TiersIntitule</c> ; le compteur de la boucle s'appelle
    /// <c>counter</c>). Les vraies valeurs vivent sur <c>IErpTiersIce</c>
    /// (<c>infoFournisseur.TiersIdentifiant</c>/<c>.TiersIce</c>), rattaché EN MÉMOIRE par
    /// <c>TiersCode</c>. Le contrôle est donc RÉÉCRIT ici, pas décommenté.
    ///
    /// <b>Différence de fond voulue.</b> Le legacy validait ligne par ligne À L'INTÉRIEUR de la boucle
    /// d'écriture ⇒ un fichier partiel était déjà écrit avant l'échec. Ici, TOUTES les lignes sont
    /// validées d'abord et le verdict complet (liste des fournisseurs fautifs) est retourné ; TASK-133
    /// n'écrit rien tant que <see cref="ResultatControleIdentiteFiscaleDelaiPaiement.EstConforme"/>
    /// est faux.
    ///
    /// <b>Règle appliquée</b> (TASK-132 §Contrôle bloquant IF/ICE) : identifiant fiscal de 8
    /// caractères sans espace, ICE de 15 caractères sans espace. Le verdict final délègue à
    /// <see cref="ValidationIdentiteFiscale.EstIfValide"/> /
    /// <see cref="ValidationIdentiteFiscale.EstIceValide"/> — POINT UNIQUE DE VÉRITÉ déjà en place
    /// dans le dépôt (aucune règle dupliquée, ARCHITECTURE §5) ; les motifs ne servent qu'à expliquer
    /// le refus. Un test d'invariant garantit que motifs et verdict ne peuvent pas diverger.
    /// </summary>
    public static class ControleIdentiteFiscaleDelaiPaiement
    {
        /// <summary>Longueur exacte exigée pour l'identifiant fiscal (legacy commenté l.80-81).</summary>
        public const int LongueurIdentifiantFiscal = 8;

        /// <summary>Longueur exacte exigée pour l'ICE (legacy commenté l.83-84).</summary>
        public const int LongueurIce = 15;

        /// <summary>
        /// Valide TOUTES les identités fournies et retourne le verdict complet. Les identités sont
        /// dédoublonnées par code tiers (un fournisseur fautif est cité UNE fois, quel que soit son
        /// nombre de lignes) et triées par code pour un message stable.
        /// </summary>
        public static ResultatControleIdentiteFiscaleDelaiPaiement Controler(
            IEnumerable<IdentiteFiscaleFournisseurDeclare> identites)
        {
            if (identites == null) throw new ArgumentNullException(nameof(identites));

            var parFournisseur = identites
                .GroupBy(i => i.TiersCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => new IdentiteFiscaleFournisseurDeclare
                {
                    TiersNo = g.First().TiersNo,
                    TiersCode = g.Key,
                    TiersIntitule = g.First().TiersIntitule,
                    IdentifiantFiscal = g.First().IdentifiantFiscal,
                    Ice = g.First().Ice,
                    TrouveDansReferentiel = g.All(x => x.TrouveDansReferentiel),
                    NombreLignes = g.Sum(x => x.NombreLignes)
                })
                .OrderBy(i => i.TiersCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fautifs = new List<FournisseurIdentiteFiscaleFautif>();

            foreach (var identite in parFournisseur)
            {
                var motifs = EvaluerMotifs(identite);
                if (motifs.Count == 0) continue;

                fautifs.Add(new FournisseurIdentiteFiscaleFautif
                {
                    TiersNo = identite.TiersNo,
                    TiersCode = identite.TiersCode,
                    TiersIntitule = identite.TiersIntitule,
                    IdentifiantFiscal = identite.IdentifiantFiscal,
                    Ice = identite.Ice,
                    NombreLignes = identite.NombreLignes,
                    Motifs = motifs,
                    MotifsLibelles = motifs.Select(m => Libelle(m, identite)).ToList()
                });
            }

            return new ResultatControleIdentiteFiscaleDelaiPaiement
            {
                NombreFournisseursExamines = parFournisseur.Count,
                NombreLignesExaminees = parFournisseur.Sum(i => i.NombreLignes),
                FournisseursFautifs = fautifs,
                MessageBloquant = ConstruireMessage(fautifs)
            };
        }

        /// <summary>
        /// Motifs d'UNE identité. Verdict final aligné sur <see cref="ValidationIdentiteFiscale"/> :
        /// si la liste est vide, alors <c>EstIfValide</c> ET <c>EstIceValide</c> sont vrais (invariant
        /// couvert par test unitaire).
        /// </summary>
        public static IReadOnlyList<MotifIdentiteFiscaleDelaiPaiement> EvaluerMotifs(IdentiteFiscaleFournisseurDeclare identite)
        {
            if (identite == null) throw new ArgumentNullException(nameof(identite));

            var motifs = new List<MotifIdentiteFiscaleDelaiPaiement>();

            if (!identite.TrouveDansReferentiel)
            {
                // Anomalie de référentiel, pas de format : on ne prétend PAS savoir si l'IF/ICE est
                // absent ou mal formé (aucune donnée lue). Motif unique, bloquant.
                motifs.Add(MotifIdentiteFiscaleDelaiPaiement.FournisseurIntrouvableDansReferentiel);
                return motifs;
            }

            AjouterMotifsChamp(
                motifs,
                identite.IdentifiantFiscal,
                LongueurIdentifiantFiscal,
                MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAbsent,
                MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide,
                MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAvecEspace);

            AjouterMotifsChamp(
                motifs,
                identite.Ice,
                LongueurIce,
                MotifIdentiteFiscaleDelaiPaiement.IceAbsent,
                MotifIdentiteFiscaleDelaiPaiement.IceLongueurInvalide,
                MotifIdentiteFiscaleDelaiPaiement.IceAvecEspace);

            return motifs;
        }

        private static void AjouterMotifsChamp(
            List<MotifIdentiteFiscaleDelaiPaiement> motifs,
            string? valeur,
            int longueurAttendue,
            MotifIdentiteFiscaleDelaiPaiement motifAbsent,
            MotifIdentiteFiscaleDelaiPaiement motifLongueur,
            MotifIdentiteFiscaleDelaiPaiement motifEspace)
        {
            if (string.IsNullOrWhiteSpace(valeur))
            {
                motifs.Add(motifAbsent);
                return;
            }

            if (valeur.Length != longueurAttendue) motifs.Add(motifLongueur);
            if (valeur.Contains(" ")) motifs.Add(motifEspace);
        }

        private static string Libelle(MotifIdentiteFiscaleDelaiPaiement motif, IdentiteFiscaleFournisseurDeclare identite)
        {
            switch (motif)
            {
                case MotifIdentiteFiscaleDelaiPaiement.FournisseurIntrouvableDansReferentiel:
                    return "fournisseur introuvable dans le référentiel tiers ERP (impossible de charger son identifiant fiscal et son ICE)";
                case MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAbsent:
                    return "identifiant fiscal absent";
                case MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide:
                    return $"identifiant fiscal de {identite.IdentifiantFiscal!.Length} caractère(s) au lieu de {LongueurIdentifiantFiscal}";
                case MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAvecEspace:
                    return "identifiant fiscal contenant un espace";
                case MotifIdentiteFiscaleDelaiPaiement.IceAbsent:
                    return "ICE absent";
                case MotifIdentiteFiscaleDelaiPaiement.IceLongueurInvalide:
                    return $"ICE de {identite.Ice!.Length} caractère(s) au lieu de {LongueurIce}";
                case MotifIdentiteFiscaleDelaiPaiement.IceAvecEspace:
                    return "ICE contenant un espace";
                default:
                    // Aucun motif silencieux : un motif non catalogué reste visible tel quel.
                    return motif.ToString();
            }
        }

        private static string ConstruireMessage(IReadOnlyList<FournisseurIdentiteFiscaleFautif> fautifs)
        {
            if (fautifs.Count == 0) return string.Empty;

            var message = new StringBuilder();
            message.Append("Génération du fichier impossible : ");
            message.Append(fautifs.Count);
            message.Append(fautifs.Count == 1
                ? " fournisseur a une identité fiscale invalide (identifiant fiscal de "
                : " fournisseurs ont une identité fiscale invalide (identifiant fiscal de ");
            message.Append(LongueurIdentifiantFiscal);
            message.Append(" caractères sans espace, ICE de ");
            message.Append(LongueurIce);
            message.Append(" caractères sans espace). Corrigez la fiche tiers dans l'ERP puis relancez la génération.");

            foreach (var fautif in fautifs)
            {
                message.AppendLine();
                message.Append(" - [");
                message.Append(fautif.TiersCode);
                message.Append("] ");
                message.Append(string.IsNullOrWhiteSpace(fautif.TiersIntitule) ? "(intitulé non renseigné)" : fautif.TiersIntitule);
                message.Append(" : ");
                message.Append(string.Join(" ; ", fautif.MotifsLibelles));
                message.Append(" (");
                message.Append(fautif.NombreLignes);
                message.Append(fautif.NombreLignes == 1 ? " ligne concernée)" : " lignes concernées)");
            }

            return message.ToString();
        }
    }
}
