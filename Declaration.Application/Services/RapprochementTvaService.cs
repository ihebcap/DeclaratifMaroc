using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;
using Declaration.Orchestration;
using Microsoft.Extensions.Configuration;

namespace Declaration.Application.Services;

public interface IRapprochementTvaService
{
    Task EnrichirTvaAsync(int soId, IReadOnlyList<ReglementRapprochementRow> rows);
}

public class RapprochementTvaService : IRapprochementTvaService
{
    private readonly IDeclarationRepository _repository;
    private readonly IVentilationSageCacheRepository? _ventilationCache;
    private readonly ILecteurTvaFgr? _lecteurFgr;
    private readonly string _grfConnectionString;
    private readonly string _sageConnectionString;
    private readonly string _persistenceConnectionString;

    public RapprochementTvaService(
        IDeclarationRepository repository,
        IVentilationSageCacheRepository? ventilationCache,
        ILecteurTvaFgr? lecteurFgr,
        IConfiguration configuration)
    {
        _repository = repository;
        _ventilationCache = ventilationCache;
        _lecteurFgr = lecteurFgr;
        _grfConnectionString = configuration.GetConnectionString("GrfConnection") 
            ?? configuration.GetConnectionString("DefaultConnection") 
            ?? "";
        _sageConnectionString = configuration.GetConnectionString("SageConnection") ?? "";
        _persistenceConnectionString = configuration.GetConnectionString("PersistenceConnection") 
            ?? configuration.GetConnectionString("DefaultConnection") 
            ?? "";
    }

    public RapprochementTvaService(
        IDeclarationRepository repository,
        IVentilationSageCacheRepository? ventilationCache,
        ILecteurTvaFgr? lecteurFgr,
        string grfConnectionString = "",
        string sageConnectionString = "",
        string persistenceConnectionString = "")
    {
        _repository = repository;
        _ventilationCache = ventilationCache;
        _lecteurFgr = lecteurFgr;
        _grfConnectionString = grfConnectionString;
        _sageConnectionString = sageConnectionString;
        _persistenceConnectionString = persistenceConnectionString;
    }

    public async Task EnrichirTvaAsync(int soId, IReadOnlyList<ReglementRapprochementRow> rows)
    {
        if (rows == null || rows.Count == 0) return;

        // Initialize default state
        foreach (var r in rows)
        {
            r.MontantTva = null;
            r.EtatValorisation = "NonApplicable";
        }

        // Filter payments that have affectations and are not pure SoldeInitial
        var reglementsAValoriser = rows
            .Where(r => r.NbAffectations > 0 && r.EcTypeMin != 4)
            .ToList();

        if (reglementsAValoriser.Count == 0) return;

        var mvNumeros = reglementsAValoriser.Select(r => r.MvNumero).Distinct().ToList();
        var affectationsList = await _repository.GetAffectationDetailsRapprochementAsync(soId, mvNumeros);
        var affectations = affectationsList
            .GroupBy(a => a.MvNumero)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Batch load cache entries for all Sage invoices (EC_Type = 0)
        var allSageEcIds = affectations.Values
            .SelectMany(list => list)
            .Where(a => a.EcType == 0)
            .Select(a => a.EcId)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<int, IReadOnlyList<VentilationSageCacheEntry>> cacheEntries = new Dictionary<int, IReadOnlyList<VentilationSageCacheEntry>>();
        if (_ventilationCache != null && allSageEcIds.Count > 0)
        {
            try
            {
                cacheEntries = _ventilationCache.GetEntriesBatch(soId, allSageEcIds, _persistenceConnectionString);
            }
            catch
            {
                // Silently swallow cache error and mark as Indisponible if cache fails
            }
        }

        foreach (var reglement in reglementsAValoriser)
        {
            if (!affectations.TryGetValue(reglement.MvNumero, out var list) || list.Count == 0)
            {
                reglement.MontantTva = null;
                reglement.EtatValorisation = "NonApplicable";
                continue;
            }

            decimal totalTvaSum = 0m;
            bool hasUnavailable = false;
            int countValorisees = 0;

            foreach (var a in list)
            {
                if (a.EcType == 4) // SoldeInitial
                {
                    continue;
                }
                else if (a.EcType == 111) // FGR
                {
                    try
                    {
                        var doc = _lecteurFgr?.LireTvaFgr(a.EcId, a.EcNumero, _grfConnectionString, _sageConnectionString);
                        if (doc != null && doc.MontantsBrutsDisponibles)
                        {
                            decimal invoiceTva = (decimal)doc.TotalTva;
                            decimal affectationTva = invoiceTva;
                            if (a.EcMontant > 0 && a.AfMontant < a.EcMontant)
                            {
                                affectationTva = Math.Round(invoiceTva * (a.AfMontant / a.EcMontant), 2);
                            }
                            totalTvaSum += affectationTva;
                            countValorisees++;
                        }
                        else
                        {
                            hasUnavailable = true;
                        }
                    }
                    catch
                    {
                        hasUnavailable = true;
                    }
                }
                else if (a.EcType == 0) // Sage
                {
                    if (cacheEntries.TryGetValue(a.EcId, out var entries) && entries != null && entries.Count > 0)
                    {
                        decimal invoiceTva = entries.Sum(e => (decimal)e.MontantTva);
                        decimal affectationTva = invoiceTva;
                        if (a.EcMontant > 0 && a.AfMontant < a.EcMontant)
                        {
                            affectationTva = Math.Round(invoiceTva * (a.AfMontant / a.EcMontant), 2);
                        }
                        totalTvaSum += affectationTva;
                        countValorisees++;
                    }
                    else
                    {
                        hasUnavailable = true;
                    }
                }
                else
                {
                    hasUnavailable = true;
                }
            }

            if (countValorisees == 0 && hasUnavailable)
            {
                reglement.MontantTva = null;
                reglement.EtatValorisation = "Indisponible";
            }
            else if (hasUnavailable)
            {
                reglement.MontantTva = totalTvaSum > 0 ? Math.Round(totalTvaSum, 2) : null;
                reglement.EtatValorisation = "Indisponible";
            }
            else
            {
                reglement.MontantTva = Math.Round(totalTvaSum, 2);
                if (Math.Abs(reglement.ResteAAffecter) > 0.005m)
                {
                    reglement.EtatValorisation = "Partielle";
                }
                else
                {
                    reglement.EtatValorisation = "Valorisee";
                }
            }
        }
    }
}
