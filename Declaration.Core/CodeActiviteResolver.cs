namespace Declaration.Core
{
    /// <summary>
    /// TASK-161/TASK-179 : point UNIQUE de résolution du code activité TVA effectif d'une ligne, en
    /// cascade, jamais bloquant :
    ///   1. Surcharge manuelle déjà posée sur la ligne (avant figeage, écran ② Vérifier &amp;
    ///      Intégrer) — prioritaire sur tout le reste, couvre le cas "même facture, deux
    ///      activités".
    ///   2. Colonne Sage désignée déjà câblée (F_COMPTET.CT_APE, cf.
    ///      Declaration.Selection.SelectionExpliqueeService.EnrichirTiersDepuisSageAsync).
    ///   3. "" — jamais de valeur inventée (décision PO TASK-161 point 3, non bloquant : le code
    ///      activité n'entre pas dans le XML de dépôt DGI).
    /// TASK-179 : le niveau "défaut par tiers" (P_SOCIETECODEACTIVITETIERS) a été retiré — n'a
    /// jamais fonctionné en réel (TASK-171), dépend d'une table winform non modifiable, et fait
    /// doublon avec le niveau CT_APE déjà fonctionnel.
    /// Utilisé par tous les points qui construisent un CodeActivite (évite la divergence historique
    /// TASK-103/108/112) : <c>Declaration.Application.Services.DeclarationWorkflowService</c>
    /// (construction/reconstruction de <c>LigneCandidate</c>),
    /// <c>Declaration.Selection.SelectionExpliqueeEvaluator</c> et
    /// <c>Declaration.Selection.SelectionnerAffectationsService</c>.
    /// </summary>
    public static class CodeActiviteResolver
    {
        public static string Resoudre(
            string? surchargeManuelle,
            string? codeActiviteSage)
        {
            if (!string.IsNullOrWhiteSpace(surchargeManuelle))
                return surchargeManuelle!;

            if (!string.IsNullOrWhiteSpace(codeActiviteSage))
                return codeActiviteSage!;

            return "";
        }
    }
}
