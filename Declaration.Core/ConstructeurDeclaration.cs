using System;
using System.Collections.Generic;
using System.Linq;
using Declaration.Core.Model;
using SageTaxReader.Contracts;

namespace Declaration.Core
{
    public static class ConstructeurDeclaration
    {
        public static DeclarationModele ConstruireDeclaration(
            IEnumerable<AffectationADeclarer> affectations,
            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFacture,
            int n)
        {
            var modele = new DeclarationModele();
            var lignesPourRecap = new List<LigneDeclarationEnrichie>();
            decimal totalMontantAffecte = 0m;
            decimal residuExplique = 0m;

            foreach (var affectation in affectations)
            {
                if (affectation.EstRapprocheNonAffecte)
                {
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Error,
                        Code = "REGLEMENT_NON_AFFECTE",
                        Message = "Règlement rapproché non affecté (affectation attendue absente).",
                        RefLigne = $"Facture/Règlement: {affectation.NumeroFacture}"
                    });
                    continue; 
                }

                // Solde initial (EC_Type=4) SANS saisie manuelle : montant connu en TTC seul, aucun
                // détail taux/TVA dans Sage — jamais de calcul deviné. Alerte ACTIONNABLE (le
                // comptable doit saisir taux + montant de TVA) plutôt que la ligne éliminée sans
                // recours (décision PO — remplace TASK-025 « non géré »). Avec saisie : flux normal
                // ci-dessous via resoudreFacture (qui construit un DocumentTaxesInfo synthétique).
                if (affectation.EC_Type == 4 && affectation.SoldeInitialTva == null)
                {
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Warning,
                        Code = "SOLDE_INITIAL_SAISIE_REQUISE",
                        Message = "Solde initial : taux et montant de TVA à saisir manuellement pour intégrer cette ligne à la déclaration (montant connu en TTC seul).",
                        RefLigne = $"Facture: {affectation.NumeroFacture}"
                    });
                    continue;
                }

                DocumentTaxesInfo? facture = null;
                try 
                {
                    facture = resoudreFacture(affectation);
                }
                catch (FgrValidationException ex)
                {
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Warning,
                        Code = "ECART_FGR",
                        Message = ex.Message,
                        RefLigne = $"Facture: {affectation.NumeroFacture}"
                    });
                    facture = ex.Document; // On garde le document partiel !
                }
                catch (InvalidOperationException ex)
                {
                    string codeAlerte = ex.Message.StartsWith("CODE_TAXE_INCONNU") ? "CODE_TAXE_INCONNU" : "ERREUR_FGR";
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Error,
                        Code = codeAlerte,
                        Message = ex.Message,
                        RefLigne = $"Facture: {affectation.NumeroFacture}"
                    });
                    continue;
                }

                if (facture == null)
                {
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Error,
                        Code = "FACTURE_INTROUVABLE",
                        // Message lu tel quel par le comptable dans « Vérifier & Intégrer » :
                        // « DTO non fourni » est du jargon développeur, il ne dit pas quoi faire.
                        Message = "Facture introuvable dans Sage : le détail TVA de cette facture n'a pas pu être lu. Vérifiez que la facture existe toujours dans Sage et relancez « Resynchroniser ».",
                        RefLigne = $"Facture: {affectation.NumeroFacture}"
                    });
                    continue; // Pas de ventilation possible
                }

                // Pièce isolée en erreur par la lecture OM en lot (TASK-023) : jamais de ligne
                // silencieuse ni de ventilation fausse — on remonte le motif exact en alerte.
                if (facture.EnErreur)
                {
                    modele.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Error,
                        Code = "FACTURE_ILLISIBLE_OM",
                        Message = string.IsNullOrWhiteSpace(facture.MotifErreur)
                            ? "Lecture OM Sage en échec pour cette pièce."
                            : facture.MotifErreur,
                        RefLigne = $"Facture: {affectation.NumeroFacture}"
                    });
                    continue; // Pas de ventilation possible
                }

                var validationAlerts = ValiderAffectation(affectation);
                modele.Alertes.AddRange(validationAlerts);

                totalMontantAffecte += affectation.MontantAffecte;
                if (facture.TotalTtc != 0)
                {
                    decimal proration = affectation.MontantAffecte / (decimal)facture.TotalTtc;
                    residuExplique += proration * (decimal)(facture.TotalParafiscale + facture.Escompte);
                }

                var ventilation = Ventilateur.Ventiler(facture, affectation.MontantAffecte, n);

                foreach (var ligne in ventilation.Lignes)
                {
                    if (ligne.Assiette == 0)
                    {
                        modele.Alertes.Add(new Alerte
                        {
                            Niveau = NiveauAlerte.Warning,
                            Code = "LIGNE_A_ZERO",
                            Message = "Ligne à 0 (assiette nulle).",
                            RefLigne = $"Facture: {affectation.NumeroFacture}, Taxe: {ligne.CodeTaxe}"
                        });
                    }

                    var codeAct = string.IsNullOrWhiteSpace(affectation.Tiers.CodeActivite) ? "(sans activité)" : affectation.Tiers.CodeActivite;
                    if (string.IsNullOrWhiteSpace(affectation.Tiers.CodeActivite))
                    {
                        // Eviter de dupliquer les alertes par ligne (on peut l'ajouter une fois par facture/tiers)
                        if (!modele.Alertes.Any(a => a.Code == "SANS_ACTIVITE" && a.RefLigne == $"Tiers: {affectation.Tiers.Numero}"))
                        {
                            modele.Alertes.Add(new Alerte
                            {
                                Niveau = NiveauAlerte.Info,
                                Code = "SANS_ACTIVITE",
                                Message = "Code activité manquant.",
                                RefLigne = $"Tiers: {affectation.Tiers.Numero}"
                            });
                        }
                    }

                    var codeTaxe = string.IsNullOrWhiteSpace(affectation.ValorisationDirecteCodeTaxe) 
                        ? (ligne.CodeTaxe ?? "") 
                        : affectation.ValorisationDirecteCodeTaxe;

                    var enrichie = new LigneDeclarationEnrichie
                    {
                        NumeroFacture = affectation.NumeroFacture,
                        NumeroRapprochement = affectation.NumeroRapprochement,
                        Designation = "", // TODO: source = worker OM, à exposer plus tard — cf. TASK-002/DTO
                        Tiers = affectation.Tiers,
                        CodeActivite = codeAct,
                        HT = ligne.Assiette,
                        Taux = ligne.Taux,
                        CodeTaxe = codeTaxe,
                        Tva = ligne.Tva,
                        Ttc = ligne.Ttc,
                        Prorata = ligne.Prorata,
                        ModePaiement = affectation.ModePaiement,
                        DatePaiement = affectation.DatePaiement,
                        DateFacture = affectation.DateFacture,
                        Reference = affectation.Reference,
                        Source = affectation.Source
                    };

                    modele.Lignes.Add(enrichie);
                    lignesPourRecap.Add(enrichie);
                }
            }

            modele.RecapsParSource = lignesPourRecap
                .GroupBy(l => l.Source)
                .Select(g => new RecapParSource
                {
                    Source = g.Key,
                    TotalHT = g.Sum(x => x.HT),
                    TotalTva = g.Sum(x => x.Tva),
                    TotalTtc = g.Sum(x => x.Ttc)
                }).ToList();

            // TASK-180 / TASK-198 : clivage Collecté / Déductible + regrouper par (Taux, CodeTaxe).
            modele.RecapsParTaux = lignesPourRecap
                .GroupBy(l => new { l.Taux, CodeTaxe = l.CodeTaxe ?? "", Collecte = l.Source == SourceAffectation.Encaissement })
                .Select(g => new RecapParTaux
                {
                    Taux = g.Key.Taux,
                    CodeTaxe = g.Key.CodeTaxe,
                    Collecte = g.Key.Collecte,
                    TotalHT = g.Sum(x => x.HT),
                    TotalTva = g.Sum(x => x.Tva),
                    TotalTtc = g.Sum(x => x.Ttc)
                }).ToList();

            modele.RecapsParActivite = lignesPourRecap
                .GroupBy(l => new { l.CodeActivite, Collecte = l.Source == SourceAffectation.Encaissement })
                .Select(g => new RecapParActivite
                {
                    CodeActivite = g.Key.CodeActivite,
                    Collecte = g.Key.Collecte,
                    TotalHT = g.Sum(x => x.HT),
                    TotalTva = g.Sum(x => x.Tva),
                    TotalTtc = g.Sum(x => x.Ttc)
                }).ToList();

            decimal totalDeclarationTtc = modele.Lignes.Sum(l => l.Ttc);
            
            modele.ControleEquilibre = new ControleEquilibre
            {
                TotalMontantAffecte = totalMontantAffecte,
                TotalDeclareTtc = totalDeclarationTtc,
                ResiduExplique = residuExplique
            };

            decimal tolerance = 0.05m * modele.Lignes.Count;
            if (Math.Abs(modele.ControleEquilibre.ResiduInexplique) > tolerance)
            {
                modele.Alertes.Add(new Alerte
                {
                    Niveau = NiveauAlerte.Warning,
                    Code = "EQUILIBRE_RESIDU_INEXPLIQUE",
                    Message = $"Résidu inexpliqué de {modele.ControleEquilibre.ResiduInexplique:F2} (tolérance: {tolerance:F2}).",
                    RefLigne = "Global"
                });
            }

            return modele;
        }

        private static IEnumerable<Alerte> ValiderAffectation(AffectationADeclarer a)
        {
            var alertes = new List<Alerte>();
            var t = a.Tiers ?? new TiersInfo();
            var refLigne = $"Facture: {a.NumeroFacture}";

            if (string.IsNullOrWhiteSpace(t.Ice))
            {
                alertes.Add(new Alerte { Niveau = NiveauAlerte.Error, Code = "TIERS_SANS_ICE", Message = "Tiers sans ICE.", RefLigne = refLigne });
            }
            else if (t.Ice.Length != 15 || t.Ice.Any(char.IsWhiteSpace))
            {
                alertes.Add(new Alerte { Niveau = NiveauAlerte.Error, Code = "ICE_INVALIDE", Message = "ICE ≠ 15 caractères ou contenant des espaces.", RefLigne = refLigne });
            }

            if (string.IsNullOrWhiteSpace(t.IdentifiantFiscal))
            {
                alertes.Add(new Alerte { Niveau = NiveauAlerte.Error, Code = "TIERS_SANS_IF", Message = "Tiers sans identifiant fiscal.", RefLigne = refLigne });
            }
            else if (t.IdentifiantFiscal.Length != 8 || t.IdentifiantFiscal.Any(char.IsWhiteSpace))
            {
                alertes.Add(new Alerte { Niveau = NiveauAlerte.Error, Code = "IF_INVALIDE", Message = "Identifiant fiscal ≠ 8 caractères ou contenant des espaces.", RefLigne = refLigne });
            }

            return alertes;
        }
    }
}
