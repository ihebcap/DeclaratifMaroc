using System;
using System.Collections.Generic;
using System.Linq;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-132 : type de déclaration Délai de Paiement, reproduit à l'identique le legacy
    /// <c>TypeDeclarationDelaisPaiement</c>
    /// (<c>apbs-gr_winform/src/Tresorerie.Core/Enum/TypeDeclarationDelaisPaiement.cs</c>) :
    /// Annuelle = 1, Trimestrielle = 2. Valeurs persistées dans
    /// <c>RT_DECLARATIONDELAISPAIEMENT.DDP_Type</c>.
    /// </summary>
    public enum TypeDeclarationDelaiPaiement
    {
        Annuelle = 1,
        Trimestrielle = 2
    }

    /// <summary>
    /// TASK-132 : trimestre déclaré, reproduit à l'identique le legacy
    /// <c>DeclarationTvaEncaissementTrimestrePeriode</c>
    /// (<c>Tresorerie.Core/Enum/DeclarationTvaEncaissementTrimestrePeriode.cs</c>, T1 = 1 … T4 = 4).
    /// Persisté dans <c>DDP_Periode</c>.
    /// </summary>
    public enum TrimestreDelaiPaiement
    {
        T1 = 1,
        T2 = 2,
        T3 = 3,
        T4 = 4
    }

    /// <summary>
    /// TASK-132 : statut de la déclaration, reproduit à l'identique le legacy <c>StatutDeclaration</c>
    /// (<c>Tresorerie.Core/Enum/StatutDeclaration.cs</c>) : EnCours = 0, Cloture = 1. Persisté dans
    /// <c>DDP_Statut</c>.
    /// </summary>
    public enum StatutDeclarationDelaiPaiement
    {
        EnCours = 0,
        Cloture = 1
    }

    /// <summary>
    /// TASK-132 : bornes de période CALCULÉES (jamais saisies librement) d'une déclaration Délai de
    /// Paiement. <see cref="DateDebut"/> est à minuit ; <see cref="DateFin"/> est le DERNIER JOUR de
    /// la période à minuit — la conversion vers la borne « fin de journée » stockée en base
    /// (<c>23:59:59</c>, convention legacy) relève de la couche de persistance, pas du calculateur.
    /// </summary>
    public sealed class PeriodeDeclarationDelaiPaiement
    {
        public DateTime DateDebut { get; init; }
        public DateTime DateFin { get; init; }
    }

    /// <summary>
    /// TASK-132 : état d'une déclaration nécessaire et suffisant pour statuer sur une transition de
    /// cycle de vie. Type PUR (aucune dépendance base) : l'appelant le projette depuis
    /// <c>RT_DECLARATIONDELAISPAIEMENT</c> + le compte de lignes
    /// <c>RT_DECLARATIONDELAISPAIEMENTLG</c>.
    /// </summary>
    public sealed class EtatDeclarationDelaiPaiement
    {
        public StatutDeclarationDelaiPaiement Statut { get; init; }

        /// <summary><c>DDP_IsDepose</c> — flag MANUEL posé après export (décision PO §5.A-2 : aucune plateforme externe).</summary>
        public bool EstDeposee { get; init; }

        /// <summary><c>DDP_IsGeneretedFile</c>.</summary>
        public bool FichierGenere { get; init; }

        /// <summary>Nombre de lignes intégrées (<c>RT_DECLARATIONDELAISPAIEMENTLG</c>) — équivalent du legacy <c>HasLignes</c>.</summary>
        public int NombreLignes { get; init; }

        public bool ADesLignes => NombreLignes > 0;
    }

    /// <summary>
    /// TASK-132 : résumé d'une déclaration existante, utilisé UNIQUEMENT par les contrôles d'unicité
    /// de période (message d'erreur explicite citant la déclaration en conflit).
    /// </summary>
    public sealed class DeclarationDelaiPaiementExistanteResume
    {
        public int DdpId { get; init; }
        public string Numero { get; init; } = string.Empty;
        public int Exercice { get; init; }
        public DateTime DateDebut { get; init; }

        /// <summary>Dernier jour de la période (partie heure ignorée par les contrôles).</summary>
        public DateTime DateFin { get; init; }
    }

    /// <summary>
    /// TASK-132 — Cycle de vie PUR de la déclaration Délai de Paiement Maroc (hors DB, testable comme
    /// <see cref="ConventionDelaiPaiementValidator"/> / <see cref="DelaiPaiementBootstrapGuard"/>).
    ///
    /// Reproduit à l'identique <c>SocieteManager.Complement.cs:601-969</c>
    /// (<c>DeclarationDelaisPaiementCreate</c>/<c>Update</c>/<c>Cloture</c>/<c>AnnulerCloture</c>/
    /// <c>Depose</c>/<c>FichierGenerer</c>/<c>FichierAnnulerGeneration</c>/<c>Delete</c>/
    /// <c>LigneAjouter</c>/<c>DeleteLigne</c>) : mêmes gardes, MÊME ORDRE d'évaluation, mêmes messages
    /// (français, y compris les tournures d'origine) — un utilisateur habitué à l'ancien applicatif
    /// retrouve exactement les mêmes blocages.
    ///
    /// Une seule divergence assumée : le type d'exception. Le legacy lève
    /// <c>ApplicationException</c> ; ici <see cref="InvalidOperationException"/>, comme
    /// <c>ConventionDelaiPaiementService</c> (TASK-129) — convention du dépôt GRF.
    ///
    /// Le contrôle IF/ICE bloquant avant génération de fichier vit à part
    /// (<see cref="ControleIdentiteFiscaleDelaiPaiement"/>) : il exige une lecture Sage
    /// (<c>F_COMPTET</c>), donc il ne peut pas être fusionné dans ces gardes purement d'état.
    /// </summary>
    public static class DeclarationDelaiPaiementCycleDeVie
    {
        /// <summary>
        /// <c>DDP_Periode</c> pour une déclaration ANNUELLE : aucun trimestre applicable.
        ///
        /// Le legacy y recopiait le trimestre résiduel du formulaire (valeur non significative, jamais
        /// relue pour une annuelle : <c>DeclarationDelaisPaiementFileGenerator.cs:59-74</c> écrit la
        /// période littérale <c>5</c> pour l'annuelle). On persiste donc 0 = « non applicable » plutôt
        /// qu'une valeur trompeuse. <b>TASK-133 doit émettre 5 pour l'annuelle, sans lire
        /// <c>DDP_Periode</c>.</b>
        /// </summary>
        public const int PeriodeNonApplicable = 0;

        /// <summary>
        /// Calcul AUTOMATIQUE des bornes de période (legacy l.617-653, bornes reproduites à
        /// l'identique) : année civile complète pour <see cref="TypeDeclarationDelaiPaiement.Annuelle"/>,
        /// trimestre calendaire pour <see cref="TypeDeclarationDelaiPaiement.Trimestrielle"/>.
        /// Les bornes ne sont JAMAIS saisies librement — c'est ce qui rend le contrôle d'unicité par
        /// égalité des bornes suffisant (cf. TASK-132 §Référence legacy).
        /// </summary>
        public static PeriodeDeclarationDelaiPaiement CalculerPeriode(
            int exercice,
            TypeDeclarationDelaiPaiement type,
            TrimestreDelaiPaiement? trimestre)
        {
            if (exercice < 1 || exercice > 9999)
                throw new InvalidOperationException($"Exercice invalide : {exercice}.");

            switch (type)
            {
                case TypeDeclarationDelaiPaiement.Annuelle:
                    return new PeriodeDeclarationDelaiPaiement
                    {
                        DateDebut = new DateTime(exercice, 1, 1),
                        DateFin = new DateTime(exercice, 12, 31)
                    };

                case TypeDeclarationDelaiPaiement.Trimestrielle:
                    if (trimestre == null)
                        throw new InvalidOperationException("Trimestre invalide.");

                    switch (trimestre.Value)
                    {
                        case TrimestreDelaiPaiement.T1:
                            return new PeriodeDeclarationDelaiPaiement
                            {
                                DateDebut = new DateTime(exercice, 1, 1),
                                DateFin = new DateTime(exercice, 3, 31)
                            };
                        case TrimestreDelaiPaiement.T2:
                            return new PeriodeDeclarationDelaiPaiement
                            {
                                DateDebut = new DateTime(exercice, 4, 1),
                                DateFin = new DateTime(exercice, 6, 30)
                            };
                        case TrimestreDelaiPaiement.T3:
                            return new PeriodeDeclarationDelaiPaiement
                            {
                                DateDebut = new DateTime(exercice, 7, 1),
                                DateFin = new DateTime(exercice, 9, 30)
                            };
                        case TrimestreDelaiPaiement.T4:
                            return new PeriodeDeclarationDelaiPaiement
                            {
                                DateDebut = new DateTime(exercice, 10, 1),
                                DateFin = new DateTime(exercice, 12, 31)
                            };
                        default:
                            throw new InvalidOperationException("Trimestre invalide.");
                    }

                default:
                    throw new InvalidOperationException("Type déclaration invalide.");
            }
        }

        /// <summary>
        /// <c>DDP_Periode</c> à persister : le trimestre pour une trimestrielle,
        /// <see cref="PeriodeNonApplicable"/> pour une annuelle.
        /// </summary>
        public static int ResoudrePeriodePersistee(TypeDeclarationDelaiPaiement type, TrimestreDelaiPaiement? trimestre)
            => type == TypeDeclarationDelaiPaiement.Trimestrielle
                ? (int)(trimestre ?? throw new InvalidOperationException("Trimestre invalide."))
                : PeriodeNonApplicable;

        /// <summary>
        /// Unicité de période (legacy l.655 + l.658-660), les DEUX contrôles reproduits :
        /// <list type="number">
        /// <item><b>égalité exacte des bornes</b> pour l'exercice — le contrôle nominal, suffisant
        /// parce que les bornes sont toujours calculées (jamais saisies) ;</item>
        /// <item><b>période englobante</b> : une déclaration existante dont la plage contient
        /// entièrement la nouvelle plage (legacy <c>x.DateDebut &lt;= dateDebut &amp;&amp; x.DateFin
        /// &gt;= dateFin</c>).</item>
        /// </list>
        /// Comparaisons faites au JOUR (partie heure ignorée) : indispensable car le legacy stocke
        /// <c>DDP_DateFin</c> à <c>23:59:59</c> — sa propre requête d'égalité exacte, interrogée à
        /// minuit, ne pouvait donc jamais matcher (contrôle mort en pratique ; cf. VERIFY TASK-132).
        ///
        /// <b>Limite connue, NON corrigée ici</b> (le texte de TASK-132 tranche explicitement « pas de
        /// vrai risque de chevauchement à gérer ici ») : le contrôle n°2 est ASYMÉTRIQUE, comme le
        /// legacy. Créer une trimestrielle alors qu'une annuelle du même exercice existe est bloqué ;
        /// l'inverse (annuelle après un trimestre) ne l'est pas. Signalé en VERIFY pour arbitrage PO,
        /// jamais durci sans décision.
        /// </summary>
        /// <returns>La première déclaration existante en conflit, ou <c>null</c>.</returns>
        public static DeclarationDelaiPaiementExistanteResume? TrouverPeriodeEnConflit(
            int exercice,
            PeriodeDeclarationDelaiPaiement periode,
            IEnumerable<DeclarationDelaiPaiementExistanteResume> declarationsExistantes)
        {
            if (periode == null) throw new ArgumentNullException(nameof(periode));
            if (declarationsExistantes == null) throw new ArgumentNullException(nameof(declarationsExistantes));

            var debut = periode.DateDebut.Date;
            var fin = periode.DateFin.Date;

            var duMemeExercice = declarationsExistantes.Where(x => x.Exercice == exercice).ToList();

            // 1) Égalité exacte des bornes calculées (legacy l.655, rendue réellement effective).
            var identique = duMemeExercice.FirstOrDefault(x => x.DateDebut.Date == debut && x.DateFin.Date == fin);
            if (identique != null) return identique;

            // 2) Période existante ENGLOBANTE (legacy l.659, reproduit tel quel, asymétrie incluse).
            return duMemeExercice.FirstOrDefault(x => x.DateDebut.Date <= debut && x.DateFin.Date >= fin);
        }

        // ─── Gardes de transition (ordre d'évaluation legacy strictement respecté) ─────────────────

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementUpdate</c> (l.686-706) : seul le libellé est modifiable,
        /// et uniquement tant que la déclaration est EnCours et non déposée.
        /// </summary>
        public static void ValiderModificationLibelle(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.EnCours) throw new InvalidOperationException("La déclaration est clôturée.");
        }

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementLigneAjouter</c> (l.891-937) : intégration de lignes
        /// possible uniquement sur une déclaration EnCours non déposée.
        /// </summary>
        public static void ValiderIntegrationLignes(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.EnCours) throw new InvalidOperationException("La déclaration est clôturée.");
        }

        /// <summary>Legacy <c>DeclarationDelaisPaiementDeleteLigne</c> (l.939-969) : mêmes gardes que l'ajout.</summary>
        public static void ValiderSuppressionLigne(EtatDeclarationDelaiPaiement etat)
            => ValiderIntegrationLignes(etat);

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementCloture</c> (l.708-732) : déposée → clôturée →
        /// <c>HasLignes</c>. La garde « au moins une ligne intégrée » est le critère TASK-132.
        /// </summary>
        public static void ValiderCloture(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.EnCours) throw new InvalidOperationException("La déclaration est clôturée.");
            if (!etat.ADesLignes) throw new InvalidOperationException("La déclaration ne contient aucune ligne.");
        }

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementAnnulerCloture</c> (l.734-758) : déclôture possible tant
        /// que le fichier n'est pas généré (et que la déclaration n'est pas déposée).
        /// </summary>
        public static void ValiderAnnulationCloture(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.FichierGenere) throw new InvalidOperationException("Le fichier de la déclaration est généré.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.Cloture) throw new InvalidOperationException("La déclaration n'est pas clôturée.");
        }

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementFichierGenerer</c> (l.790-827) : garde d'ÉTAT uniquement.
        /// Le contrôle IF/ICE bloquant (<see cref="ControleIdentiteFiscaleDelaiPaiement"/>) s'ajoute
        /// par-dessus, côté service, car il exige une lecture Sage.
        /// </summary>
        public static void ValiderGenerationFichier(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.FichierGenere) throw new InvalidOperationException("Le fichier du déclaration est déjà généré.");
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.Cloture) throw new InvalidOperationException("La déclaration n'est pas clôturée.");
            if (!etat.ADesLignes) throw new InvalidOperationException("La déclaration ne contient aucune ligne.");
        }

        /// <summary>Legacy <c>DeclarationDelaisPaiementFichierAnnulerGeneration</c> (l.829-866).</summary>
        public static void ValiderAnnulationGenerationFichier(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (!etat.FichierGenere) throw new InvalidOperationException("Le fichier du déclaration n'est pas généré.");
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.Cloture) throw new InvalidOperationException("La déclaration n'est pas clôturée.");
            if (!etat.ADesLignes) throw new InvalidOperationException("La déclaration ne contient aucune ligne.");
        }

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementDepose</c> (l.760-788) : dépôt = FLAG MANUEL posé APRÈS
        /// export (décision PO §5.A-2 du 19/07/2026, aucune intégration de plateforme externe) — d'où
        /// la garde « fichier généré » en premier.
        /// </summary>
        public static void ValiderDepot(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (!etat.FichierGenere) throw new InvalidOperationException("Le fichier du déclaration n'est pas généré.");
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.Cloture) throw new InvalidOperationException("La déclaration n'est pas clôturée.");
            if (!etat.ADesLignes) throw new InvalidOperationException("La déclaration ne contient aucune ligne.");
        }

        /// <summary>
        /// Legacy <c>DeclarationDelaisPaiementDelete</c> (l.868-889) : suppression possible uniquement
        /// EnCours, non déposée, et SANS aucune ligne intégrée (les lignes doivent être retirées
        /// d'abord — aucune suppression en cascade silencieuse, ARCHITECTURE §5).
        /// </summary>
        public static void ValiderSuppression(EtatDeclarationDelaiPaiement etat)
        {
            Exiger(etat);
            if (etat.EstDeposee) throw new InvalidOperationException("La déclaration est déposée.");
            if (etat.Statut != StatutDeclarationDelaiPaiement.EnCours) throw new InvalidOperationException("La déclaration est clôturée.");
            if (etat.ADesLignes) throw new InvalidOperationException("La déclaration contient des lignes.");
        }

        private static void Exiger(EtatDeclarationDelaiPaiement etat)
        {
            if (etat == null) throw new ArgumentNullException(nameof(etat));
        }
    }
}
