using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Core.Model;
using Declaration.Orchestration;
using Declaration.Selection;

namespace Declaration.Controle;

public class ComparateurDeclarationService
{
    private readonly IGrfnDeclarationRepository _grfnRepository;
    private readonly ISelectionnerAffectationsService _selectionService;
    private readonly OrchestrateurDeclaration _orchestrateur;

    public ComparateurDeclarationService(
        IGrfnDeclarationRepository grfnRepository,
        ISelectionnerAffectationsService selectionService,
        OrchestrateurDeclaration orchestrateur)
    {
        _grfnRepository = grfnRepository;
        _selectionService = selectionService;
        _orchestrateur = orchestrateur;
    }

    public async Task<RapportEcarts> Comparer(int dtId, string connectionString)
    {
        var info = await _grfnRepository.GetDeclarationInfoAsync(dtId);
        if (info == null)
        {
            throw new Exception($"Déclaration GRFN {dtId} introuvable.");
        }

        if (!int.TryParse(info.SocieteId, out int soId))
        {
            throw new Exception($"SocieteId {info.SocieteId} n'est pas un entier valide.");
        }

        // 1. Charger lignes GRFN stockées
        var lignesGrfn = (await _grfnRepository.GetLignesDeclarationAsync(dtId)).ToList();

        // 2. Recalculer via pipeline corrigé
        var affectations = await _selectionService.SelectionnerAffectationsAsync(soId, info.DateDebut, info.DateFin, connectionString, dtId);
        var modele = _orchestrateur.Traiter(affectations, 2); // 2 décimales par défaut pour le recalcul

        var lignesRecalculees = modele.Lignes;

        // 3. Aligner par clé (NumeroFacture + Taux + NumeroRapprochement)
        var rapport = new RapportEcarts();

        var grfnMap = new Dictionary<string, LigneDeclarationGrfn>();
        foreach (var lg in lignesGrfn)
        {
            var key = $"{lg.DTL_DocNumero}|{lg.DTL_Taux}|{lg.DTL_MvNumero}";
            grfnMap[key] = lg; // Note: possible duplication in real data if multiple same-rate affectations on same move
        }

        var recalculMap = new Dictionary<string, LigneDeclarationEnrichie>();
        foreach (var lr in lignesRecalculees)
        {
            var key = $"{lr.NumeroFacture}|{lr.Taux}|{lr.NumeroRapprochement}";
            recalculMap[key] = lr;
        }

        var toutesCles = grfnMap.Keys.Union(recalculMap.Keys).Distinct();
        decimal tolerance = 0.05m; // tolérance d'arrondi paramétrable

        foreach (var key in toutesCles)
        {
            var aGrfn = grfnMap.TryGetValue(key, out var lg);
            var aRecalcul = recalculMap.TryGetValue(key, out var lr);

            var ecart = new LigneEcart
            {
                NumeroFacture = lg?.DTL_DocNumero ?? lr?.NumeroFacture ?? "",
                Taux = lg?.DTL_Taux ?? lr?.Taux ?? 0m,
                NumeroRapprochement = lg?.DTL_MvNumero ?? lr?.NumeroRapprochement ?? "",
                LigneGrfn = lg,
                LigneRecalculee = lr
            };

            if (aGrfn && aRecalcul)
            {
                if (Math.Abs(ecart.EcartTVA) <= tolerance && Math.Abs(ecart.EcartAssiette) <= tolerance)
                {
                    ecart.Type = TypeEcart.Concordant;
                }
                else
                {
                    ecart.Type = TypeEcart.EcartMontant;
                    TyperCauseEcartMontant(ecart, lg!, lr!);
                }
            }
            else if (aGrfn && !aRecalcul)
            {
                ecart.Type = TypeEcart.ManquantRecalcul;
                ecart.CauseHypothetique = "Écart humain ou saut silencieux nouveau module";
            }
            else if (!aGrfn && aRecalcul)
            {
                ecart.Type = TypeEcart.ManquantGRFN;
                TyperCauseManquantGRFN(ecart, lr!);
            }

            rapport.Lignes.Add(ecart);
        }

        return rapport;
    }

    private void TyperCauseEcartMontant(LigneEcart ecart, LigneDeclarationGrfn lg, LigneDeclarationEnrichie lr)
    {
        // Différence d'arrondi ou autre ?
        if (Math.Abs(ecart.EcartTVA) <= 0.1m)
        {
            ecart.CauseHypothetique = "Arrondi: ToEven vs AwayFromZero ou erreur ratio GRFN.";
        }
        else
        {
            ecart.CauseHypothetique = "Différence de base (taxe modifiée dans SAGE ?)";
        }
    }

    private void TyperCauseManquantGRFN(LigneEcart ecart, LigneDeclarationEnrichie lr)
    {
        // Causes possibles de saut silencieux GRFN
        if (lr.IsReport)
        {
            ecart.CauseHypothetique = "Report (IsReport=1)";
        }
        else
        {
            ecart.CauseHypothetique = "Saut silencieux ancien module (espèce hors période, pointage manquant ?)";
        }
    }
}
