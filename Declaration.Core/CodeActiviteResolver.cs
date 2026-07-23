using System.Collections.Generic;

namespace Declaration.Core
{
    /// <summary>
    /// TASK-161 : point UNIQUE de résolution du code activité TVA effectif d'une ligne, en
    /// cascade, jamais bloquant :
    ///   1. Surcharge manuelle déjà posée sur la ligne (avant figeage, écran ② Vérifier &amp;
    ///      Intégrer) — prioritaire sur tout le reste, couvre le cas "même facture, deux
    ///      activités".
    ///   2. Défaut par tiers (P_SOCIETECODEACTIVITETIERS, lecture seule) — matching par numéro
    ///      tiers Sage si renseigné (clé fiable, TASK-161 point 1), sinon par intitulé ERP libre
    ///      en repli.
    ///   3. Colonne Sage désignée déjà câblée (F_COMPTET.CT_APE, cf.
    ///      Declaration.Selection.SelectionExpliqueeService.EnrichirTiersDepuisSageAsync).
    ///   4. "" — jamais de valeur inventée (décision PO TASK-161 point 3, non bloquant : le code
    ///      activité n'entre pas dans le XML de dépôt DGI).
    /// Utilisé par tous les points qui construisent un CodeActivite (évite la divergence historique
    /// TASK-103/108/112) : <c>Declaration.Application.Services.DeclarationWorkflowService</c>
    /// (construction/reconstruction de <c>LigneCandidate</c>),
    /// <c>Declaration.Selection.SelectionExpliqueeEvaluator</c> et
    /// <c>Declaration.Selection.SelectionnerAffectationsService</c> (niveaux 3/4 uniquement — ces
    /// deux derniers n'ont pas accès au mapping tiers ni à une éventuelle surcharge ligne, qui
    /// n'existent qu'au niveau de la déclaration/DM_LGTVA).
    /// </summary>
    public static class CodeActiviteResolver
    {
        public static string Resoudre(
            string? surchargeManuelle,
            string? tiersNumero,
            string? tiersNom,
            string? codeActiviteSage,
            IReadOnlyDictionary<string, string>? mappingParNumero = null,
            IReadOnlyDictionary<string, string>? mappingParNom = null)
        {
            if (!string.IsNullOrWhiteSpace(surchargeManuelle))
                return surchargeManuelle!;

            if (!string.IsNullOrWhiteSpace(tiersNumero) && mappingParNumero != null
                && mappingParNumero.TryGetValue(tiersNumero!, out var parNumero)
                && !string.IsNullOrWhiteSpace(parNumero))
                return parNumero;

            if (!string.IsNullOrWhiteSpace(tiersNom) && mappingParNom != null
                && mappingParNom.TryGetValue(tiersNom!, out var parNom)
                && !string.IsNullOrWhiteSpace(parNom))
                return parNom;

            if (!string.IsNullOrWhiteSpace(codeActiviteSage))
                return codeActiviteSage!;

            return "";
        }
    }
}
