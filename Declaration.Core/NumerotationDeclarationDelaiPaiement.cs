using System;
using System.Globalization;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-132 : paramétrage de numérotation des déclarations Délai de Paiement, tel qu'il existe
    /// DÉJÀ par société dans <c>P_SOCIETE</c> (colonnes <c>SO_DecDPPrefix</c>,
    /// <c>SO_DecDPNumAnnee</c>, <c>SO_DecDPNumMois</c>, <c>SO_DecDPNumCount</c> — créées par la
    /// migration legacy <c>202501311347278_AddTableDeclarationDelaisPaiement</c>). Lu en LECTURE
    /// SEULE : aucune colonne ajoutée, aucun schéma modifié.
    /// </summary>
    public sealed class ConfigurationNumerotationDelaiPaiement
    {
        /// <summary><c>SO_DecDPPrefix</c> (peut être vide).</summary>
        public string? Prefixe { get; init; }

        /// <summary><c>SO_DecDPNumAnnee</c> : inclure l'année sur 2 chiffres.</summary>
        public bool InclureAnnee { get; init; }

        /// <summary><c>SO_DecDPNumMois</c> : inclure le mois sur 2 chiffres.</summary>
        public bool InclureMois { get; init; }

        /// <summary><c>SO_DecDPNumCount</c> : nombre de chiffres du compteur.</summary>
        public int NombreChiffres { get; init; }
    }

    /// <summary>
    /// TASK-132 — Numérotation PURE des déclarations Délai de Paiement, reproduisant le legacy
    /// <c>SocieteRepository.GetNumeroPieceCourante</c> (l.143-312) + <c>IncrementNumero</c>
    /// (l.543-555) pour <c>EntityNumerotation.DeclarationDelaisPaiement</c> (l.513-520) :
    /// <c>préfixe + [aa] + [MM] + compteur zéro-padé</c>, compteur repris du <c>MAX(DDP_Numero)</c>
    /// des numéros de la société correspondant au même motif.
    ///
    /// <b>Pourquoi c'est ici et pas dans l'UI.</b> Le legacy calculait le numéro dans le contrôleur
    /// d'écran (<c>ListDeclarationDelaisPaiementController.InitView</c>) et le passait en paramètre à
    /// <c>DeclarationDelaisPaiementCreate</c>. TASK-132 prend en entrée « société, exercice, type,
    /// trimestre, libellé » — PAS de numéro — et <c>DDP_Numero</c> est NOT NULL : la numérotation est
    /// donc assumée par le service de création (côté serveur, jamais l'UI — ARCHITECTURE §5 « logique
    /// métier interdite dans la couche UI »). Effet de bord positif : la fenêtre de course du legacy
    /// (numéro calculé à l'ouverture du formulaire, unicité revérifiée bien plus tard) est réduite au
    /// temps d'une transaction. Elle n'est pas ANNULÉE : la table n'a aucun index unique et la
    /// contrainte de schéma interdit d'en ajouter un (table possédée par apbs-gr_winform) — limite
    /// documentée en VERIFY.
    ///
    /// <b>Durcissements assumés par rapport au legacy</b> (le legacy produisait silencieusement des
    /// numéros cassés dans ces cas) : compteur à 0 chiffre refusé, suffixe non numérique refusé,
    /// débordement du compteur refusé, dépassement des 30 caractères de <c>DDP_Numero</c> refusé.
    /// Toujours une erreur explicite, jamais un numéro douteux.
    /// </summary>
    public static class NumerotationDeclarationDelaiPaiement
    {
        /// <summary>Longueur maximale de <c>RT_DECLARATIONDELAISPAIEMENT.DDP_Numero</c> (nvarchar(30)).</summary>
        public const int LongueurMaximaleNumero = 30;

        /// <summary>
        /// Racine du numéro (tout sauf le compteur) : <c>préfixe + [aa] + [MM]</c>, dérivée de la date
        /// de référence de la déclaration.
        /// </summary>
        public static string ConstruireRacine(ConfigurationNumerotationDelaiPaiement configuration, DateTime dateReference)
        {
            Valider(configuration);

            var racine = configuration.Prefixe ?? string.Empty;
            if (configuration.InclureAnnee) racine += (dateReference.Year % 100).ToString("00", CultureInfo.InvariantCulture);
            if (configuration.InclureMois) racine += dateReference.Month.ToString("00", CultureInfo.InvariantCulture);
            return racine;
        }

        /// <summary>
        /// Motif <c>LIKE</c> SQL des numéros déjà attribués pour cette racine : <c>racine</c> suivi de
        /// <c>NombreChiffres</c> classes <c>[0-9]</c> (legacy l.160-170). Aucun caractère joker
        /// utilisateur n'entre ici : la racine provient de <c>P_SOCIETE</c>, jamais d'une saisie
        /// d'écran, et la valeur reste passée en PARAMÈTRE Dapper côté repository.
        /// </summary>
        public static string ConstruirePatternLike(ConfigurationNumerotationDelaiPaiement configuration, DateTime dateReference)
        {
            var racine = ConstruireRacine(configuration, dateReference);
            var motif = racine;
            for (var i = 0; i < configuration.NombreChiffres; i++) motif += "[0-9]";
            return motif;
        }

        /// <summary>
        /// Prochain numéro à attribuer. <paramref name="dernierNumero"/> = <c>MAX(DDP_Numero)</c> lu en
        /// base pour ce motif (<c>null</c> = aucun numéro encore attribué ⇒ compteur à 1, legacy
        /// l.302-311).
        /// </summary>
        public static string ResoudreProchainNumero(
            ConfigurationNumerotationDelaiPaiement configuration,
            DateTime dateReference,
            string? dernierNumero)
        {
            var racine = ConstruireRacine(configuration, dateReference);
            var chiffres = configuration.NombreChiffres;

            var compteur = 1;
            if (!string.IsNullOrWhiteSpace(dernierNumero))
            {
                var numero = dernierNumero.Trim();
                if (numero.Length < chiffres)
                    throw new InvalidOperationException(
                        $"Numérotation des déclarations Délai de Paiement : le dernier numéro attribué « {numero} » " +
                        $"est plus court que le compteur configuré ({chiffres} chiffres). Vérifiez le paramétrage de la société.");

                var suffixe = numero.Substring(numero.Length - chiffres, chiffres);
                if (!int.TryParse(suffixe, NumberStyles.None, CultureInfo.InvariantCulture, out var precedent))
                    throw new InvalidOperationException(
                        $"Numérotation des déclarations Délai de Paiement : le compteur « {suffixe} » du dernier numéro " +
                        $"« {numero} » n'est pas numérique. Vérifiez les numéros existants de la société.");

                compteur = precedent + 1;
            }

            var maxCompteur = (int)Math.Pow(10, chiffres) - 1;
            if (compteur > maxCompteur)
                throw new InvalidOperationException(
                    $"Numérotation des déclarations Délai de Paiement : le compteur a atteint son maximum " +
                    $"({maxCompteur}) pour le motif « {racine} ». Augmentez le nombre de chiffres (P_SOCIETE.SO_DecDPNumCount) " +
                    "ou activez l'année/le mois dans le numéro.");

            var resultat = racine + compteur.ToString(new string('0', chiffres), CultureInfo.InvariantCulture);

            if (resultat.Length > LongueurMaximaleNumero)
                throw new InvalidOperationException(
                    $"Numérotation des déclarations Délai de Paiement : le numéro « {resultat} » dépasse " +
                    $"{LongueurMaximaleNumero} caractères (limite de RT_DECLARATIONDELAISPAIEMENT.DDP_Numero). " +
                    "Raccourcissez le préfixe (P_SOCIETE.SO_DecDPPrefix).");

            return resultat;
        }

        private static void Valider(ConfigurationNumerotationDelaiPaiement configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Le legacy acceptait 0 et produisait alors un numéro sans compteur, non incrémentable
            // (le MAX suivant redonnait le même numéro). Refus EXPLICITE plutôt que numéro cassé.
            if (configuration.NombreChiffres < 1 || configuration.NombreChiffres > 9)
                throw new InvalidOperationException(
                    "Numérotation des déclarations Délai de Paiement non configurée pour cette société : " +
                    "P_SOCIETE.SO_DecDPNumCount doit valoir entre 1 et 9 (nombre de chiffres du compteur). " +
                    $"Valeur lue : {configuration.NombreChiffres}.");
        }
    }
}
