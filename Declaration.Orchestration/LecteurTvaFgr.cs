using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SageTaxReader.Contracts;
using Declaration.Core;

namespace Declaration.Orchestration
{
    public class LecteurTvaFgr : ILecteurTvaFgr
    {
        public DocumentTaxesInfo? LireTvaFgr(int ecId, string numeroFacture, string connectionString, string sageConnectionString)
        {
            var doc = new DocumentTaxesInfo
            {
                NumeroPiece = numeroFacture,
                Sens = "Achat",
                LignesTaxe = new List<TaxeDetail>()
            };

            var lignes = GetLignes(ecId, connectionString);
            var taxes = GetTaxes(sageConnectionString);
            var byIndice = lignes.GroupBy(l => l.HC_Indice).ToList();

            var indice1 = byIndice.FirstOrDefault(g => g.Key == 1);
            if (indice1 != null)
            {
                doc.TotalTtc = (double)indice1.Sum(x => x.HC_Montant);
            }

            foreach (var g in byIndice.Where(g => g.Key >= 2))
            {
                var list = g.ToList();
                if (list.Count == 2)
                {
                    var htRow = list.FirstOrDefault(l => !string.IsNullOrEmpty(l.HC_TaxeCode));
                    var tvaRow = list.FirstOrDefault(l => string.IsNullOrEmpty(l.HC_TaxeCode));

                    if (htRow != null && tvaRow != null)
                    {
                        var codeTaxe = htRow.HC_TaxeCode ?? "";
                        
                        if (taxes.TryGetValue(codeTaxe, out var tInfo))
                        {
                            doc.LignesTaxe.Add(new TaxeDetail
                            {
                                Taux = tInfo.Taux,
                                BaseHT = (double)htRow.HC_Montant,
                                MontantTva = (double)tvaRow.HC_Montant,
                                TTC = (double)(htRow.HC_Montant + tvaRow.HC_Montant),
                                Code = codeTaxe,
                                Intitule = tInfo.Intitule,
                                Type = "TaxeTypeTVA"
                            });
                        }
                        else
                        {
                            throw new InvalidOperationException($"CODE_TAXE_INCONNU : le code taxe '{codeTaxe}' n'est pas présent dans F_TAXE (bucket {g.Key})");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Structure de bucket invalide (2 lignes mais HT/TVA non identifiés) pour l'indice {g.Key}");
                    }
                }
                else if (list.Count == 1)
                {
                    var exoRow = list.First();
                    if (string.IsNullOrEmpty(exoRow.HC_TaxeCode))
                    {
                        doc.LignesTaxe.Add(new TaxeDetail
                        {
                            Taux = 0,
                            BaseHT = (double)exoRow.HC_Montant,
                            MontantTva = 0,
                            TTC = (double)exoRow.HC_Montant,
                            Code = "EXO",
                            Intitule = "Exonéré",
                            Type = "TaxeTypeTVA"
                        });
                    }
                    else
                    {
                        throw new InvalidOperationException($"Structure de bucket invalide (1 ligne avec code taxe) pour l'indice {g.Key}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Structure de bucket invalide ({list.Count} lignes) pour l'indice {g.Key}");
                }
            }

            doc.TotalHT = doc.LignesTaxe.Sum(l => l.BaseHT);
            doc.TotalTva = doc.LignesTaxe.Sum(l => l.MontantTva);

            // Validation Rule 1: Σ(HT + TVA) = TTC(indice 1)
            if (Math.Abs(doc.TotalTtc - (doc.TotalHT + doc.TotalTva)) > 0.01)
            {
                throw new FgrValidationException($"Écart de validation FGR : Σ(HT + TVA) = {doc.TotalHT + doc.TotalTva} différent du TTC (indice 1) = {doc.TotalTtc}", doc);
            }

            return doc;
        }

        protected virtual IEnumerable<HistoComptaRow> GetLignes(int ecId, string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var sql = @"
                SELECT 
                    HC_Indice, 
                    HC_Montant, 
                    HC_TaxeCode 
                FROM RT_HISTCOMPTA 
                WHERE MV_Id = @EcId
            ";
            return conn.Query<HistoComptaRow>(sql, new { EcId = ecId });
        }

        private static Dictionary<string, (double Taux, string Intitule)>? _taxesCache;
        private static readonly object _cacheLock = new object();

        protected virtual Dictionary<string, (double Taux, string Intitule)> GetTaxes(string sageConnectionString)
        {
            if (_taxesCache != null) return _taxesCache;
            lock (_cacheLock)
            {
                if (_taxesCache != null) return _taxesCache;
                using var conn = new SqlConnection(sageConnectionString);
                conn.Open();
                var sql = "SELECT TA_Code, TA_Taux, TA_Intitule FROM F_TAXE";
                var result = conn.Query(sql);
                var dict = new Dictionary<string, (double Taux, string Intitule)>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in result)
                {
                    if (row.TA_Code != null && row.TA_Taux != null)
                    {
                        string code = row.TA_Code.ToString();
                        double taux = Convert.ToDouble(row.TA_Taux);
                        string intitule = row.TA_Intitule != null ? row.TA_Intitule.ToString() : "";
                        dict[code] = (taux, intitule);
                    }
                }
                _taxesCache = dict;
                return _taxesCache;
            }
        }

        public class HistoComptaRow
        {
            public int HC_Indice { get; set; }
            public decimal HC_Montant { get; set; }
            public string HC_TaxeCode { get; set; } = "";
        }
    }
}
