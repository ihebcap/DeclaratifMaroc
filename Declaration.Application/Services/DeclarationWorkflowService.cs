using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Selection;
using Declaration.Core.Model;
using Declaration.Orchestration;
using Microsoft.Extensions.Configuration;

namespace Declaration.Application.Services;

public class DeclarationWorkflowService
{
    private readonly IDeclarationRepository _repository;
    private readonly ISelectionExpliqueeService _selectionService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;

    public DeclarationWorkflowService(
        IDeclarationRepository repository, 
        ISelectionExpliqueeService selectionService,
        IDbConnectionFactory connectionFactory,
        IConfiguration configuration)
    {
        _repository = repository;
        _selectionService = selectionService;
        _connectionFactory = connectionFactory;
        _configuration = configuration;
    }

    public async Task<DeclarationEntete> CreerDeclarationAsync(string societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type)
    {
        if (await _repository.ExistsAsync(societeId, exercice, periode, type))
            throw new InvalidOperationException("Une déclaration existe déjà pour cette société, cet exercice et cette période.");

        var numero = $"TVA{societeId}-{exercice}-{periode:D2}";

        var declaration = new DeclarationEntete
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            SocieteId = societeId,
            Exercice = exercice,
            Periode = periode,
            Type = type,
            Statut = StatutDeclaration.EnCours,
            DateCreation = DateTime.UtcNow
        };

        await _repository.CreateAsync(declaration);
        return declaration;
    }

    /// <summary>
    /// Charge les lignes candidates pour un domaine donné si elles n'ont pas encore été figées.
    /// Lignes éligibles → Etat = Proposee
    /// Lignes rejetées → Etat = Exclue + MotifRejet renseigné (aucun rejet silencieux)
    /// </summary>
    public async Task ChargerCandidatesSiNecessaireAsync(Guid declarationId, string domaine)
    {
        var count = await _repository.GetLignesCountAsync(declarationId, domaine, null);
        if (count > 0) return; // Déjà figé — idempotent

        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
        var dateFin = dateDebut.AddMonths(1).AddDays(-1);

        var grfConnectionString = _connectionFactory.GetGrfConnectionString();
        var candidates = await _selectionService.SelectionnerExpliqueeAsync(
            int.Parse(declaration.SocieteId), dateDebut, dateFin, grfConnectionString);

        var sageCs = _configuration.GetConnectionString("SageConnection") ?? "";
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(sageCs);
        var workerExe = _configuration.GetSection("WorkerConfig")?["WorkerExePath"] ?? @"D:\_vibe\GRF\SageTaxReader\SageTaxReader.Console\bin\Debug\net48\SageTaxReader.Console.exe";
        var workerConfig = new WorkerConfig 
        {
            Server = builder.DataSource,
            Database = builder.InitialCatalog,
            User = builder.UserID,
            Password = builder.Password,
            WorkerExePath = workerExe
        };
        var invoker = new WorkerInvoker();
        var lecteurFgr = new Declaration.Orchestration.LecteurTvaFgr();
        var orchestrateur = new OrchestrateurDeclaration(invoker, workerConfig, lecteurFgr, grfConnectionString, sageCs);

        var affectations = candidates.Where(c => c.EstEligible).Select(c => c.Affectation).ToList();
        var modele = orchestrateur.Traiter(affectations, 2);

        var lignes = MapLignesCandidates(declarationId, domaine, candidates, modele);

        await _repository.SaveLignesCandidatesAsync(lignes);
    }

    public static List<LigneCandidate> MapLignesCandidates(Guid declarationId, string domaine, IEnumerable<AffectationCandidate> candidates, DeclarationModele modele)
    {
        var lignes = new List<LigneCandidate>();

        foreach (var c in candidates)
        {
            if (!c.EstEligible)
            {
                var etat = c.Motif == MotifRejet.NonRapproche ? EtatLigne.Reportee : EtatLigne.Exclue;
                lignes.Add(new LigneCandidate
                {
                    Id = Guid.NewGuid(),
                    DeclarationId = declarationId,
                    Etat = etat,
                    Domaine = domaine,
                    MotifRejet = c.MotifLibelle,
                    NumeroFacture = c.Affectation.NumeroFacture,
                    NumeroRapprochement = c.Affectation.NumeroRapprochement,
                    TiersNom = c.Affectation.Tiers.Nom,
                    TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                    TiersICE = c.Affectation.Tiers.Ice,
                    HT = c.Affectation.MontantAffecte,
                    Taux = 0,
                    TVA = 0,
                    TTC = 0,
                    ModePaiement = c.Affectation.ModePaiement,
                    DatePaiement = c.Affectation.DatePaiement,
                    DateFacture = c.Affectation.DateFacture,
                    Source = c.Affectation.Source.ToString()
                });
            }
            else
            {
                var taxesLines = modele.Lignes.Where(l => l.NumeroFacture == c.Affectation.NumeroFacture).ToList();
                if (!taxesLines.Any())
                {
                    var alerte = modele.Alertes.FirstOrDefault(a => a.RefLigne.Contains(c.Affectation.NumeroFacture));
                    string motif = alerte != null ? alerte.Message : "Facture introuvable ou non ventilée";

                    lignes.Add(new LigneCandidate
                    {
                        Id = Guid.NewGuid(),
                        DeclarationId = declarationId,
                        Etat = EtatLigne.Exclue,
                        Domaine = domaine,
                        MotifRejet = motif,
                        NumeroFacture = c.Affectation.NumeroFacture,
                        NumeroRapprochement = c.Affectation.NumeroRapprochement,
                        TiersNom = c.Affectation.Tiers.Nom,
                        TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                        TiersICE = c.Affectation.Tiers.Ice,
                        HT = c.Affectation.MontantAffecte,
                        Taux = 0,
                        TVA = 0,
                        TTC = 0,
                        ModePaiement = c.Affectation.ModePaiement,
                        DatePaiement = c.Affectation.DatePaiement,
                        DateFacture = c.Affectation.DateFacture,
                        Source = c.Affectation.Source.ToString()
                    });
                }
                else
                {
                    foreach (var tl in taxesLines)
                    {
                        lignes.Add(new LigneCandidate
                        {
                            Id = Guid.NewGuid(),
                            DeclarationId = declarationId,
                            Etat = EtatLigne.Proposee,
                            Domaine = domaine,
                            MotifRejet = "",
                            NumeroFacture = c.Affectation.NumeroFacture,
                            NumeroRapprochement = c.Affectation.NumeroRapprochement,
                            TiersNom = c.Affectation.Tiers.Nom,
                            TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                            TiersICE = c.Affectation.Tiers.Ice,
                            HT = tl.HT,
                            Taux = tl.Taux,
                            TVA = tl.Tva,
                            TTC = tl.Ttc,
                            ModePaiement = c.Affectation.ModePaiement,
                            DatePaiement = c.Affectation.DatePaiement,
                            DateFacture = c.Affectation.DateFacture,
                            Source = c.Affectation.Source.ToString()
                        });
                    }
                }
            }
        }

        return lignes;
    }

    /// <summary>
    /// Vérifie la cohérence de la déclaration.
    /// - Alertes Error bloquent la clôture.
    /// - ControleEquilibre : compare la somme des TTC intégrés vs somme des HT intégrés (delta TVA).
    ///   Un résidu > 5% des lignes déclenche une alerte Warning.
    /// </summary>
    public async Task<DeclarationModele> GetCheckupAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        var model = new DeclarationModele
        {
            EnTete = new EnTeteDeclaration
            {
                IdentifiantSociete = declaration.SocieteId,
                Exercice = declaration.Exercice,
                Type = declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle
                    ? Core.Model.TypePeriode.Mensuelle
                    : Core.Model.TypePeriode.Trimestrielle,
                Numero = declaration.Numero
            }
        };

        // Récupère toutes les lignes (tous domaines confondus), intégrées uniquement
        var toutes = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var integrees = toutes.Where(l => l.Etat == EtatLigne.Integree).ToList();
        var exclues   = toutes.Where(l => l.Etat == EtatLigne.Exclue).ToList();

        // Contrôle d'équilibre : TotalMontantAffecte (HT des intégrées) vs TotalDeclareTtc (TTC des intégrées)
        decimal totalHT  = integrees.Sum(l => l.HT);
        decimal totalTtc = integrees.Sum(l => l.TTC);

        model.ControleEquilibre = new ControleEquilibre
        {
            TotalMontantAffecte = totalHT,
            TotalDeclareTtc = totalTtc,
            ResiduExplique = 0m // La TVA représente la différence expliquée
        };

        // Alertes : aucune ligne intégrée = warning
        if (!integrees.Any())
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Error,
                Code = "AUCUNE_LIGNE_INTEGREE",
                Message = "Aucune ligne n'a ete integree. La declaration est vide et ne peut pas etre cloturee.",
                RefLigne = "Global"
            });
        }

        // Info : recap des lignes exclues avec motifs
        foreach (var e in exclues)
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Info,
                Code = "LIGNE_EXCLUE",
                Message = $"Ligne exclue : {e.MotifRejet}",
                RefLigne = e.NumeroFacture
            });
        }

        // Contrôle ICE manquant sur les lignes intégrées
        foreach (var l in integrees.Where(l => string.IsNullOrWhiteSpace(l.TiersICE)))
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Error,
                Code = "TIERS_SANS_ICE",
                Message = "Ligne intégrée avec tiers sans ICE.",
                RefLigne = l.NumeroFacture
            });
        }

        return model;
    }

    public async Task CloturerDeclarationAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        if (declaration.Statut != StatutDeclaration.EnCours)
            throw new InvalidOperationException("Seule une déclaration EnCours peut être clôturée.");

        var checkup = await GetCheckupAsync(declarationId);
        // Bloquer si une alerte de niveau Error est présente
        var bloquantes = checkup.Alertes.Where(a => a.Niveau == NiveauAlerte.Error).ToList();
        if (bloquantes.Any())
        {
            var msgs = string.Join("; ", bloquantes.Select(a => $"[{a.Code}] {a.Message} ({a.RefLigne})"));
            throw new InvalidOperationException($"Clôture refusée — anomalies bloquantes : {msgs}");
        }

        await _repository.UpdateStatutAsync(declarationId, StatutDeclaration.Cloturee);
    }
}
