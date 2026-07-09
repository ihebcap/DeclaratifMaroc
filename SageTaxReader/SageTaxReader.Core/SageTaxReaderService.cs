using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Objets100cLib;
using SageTaxReader.Contracts;

namespace SageTaxReader.Core;

public class DeviseSocieteInfo
{
    public string Intitule { get; set; } = "";
    public int Decimales { get; set; }
}

public class SageTaxReaderService
{
    private readonly string _server;
    private readonly string _database;
    private readonly string _user;
    private readonly string _password;

    public SageTaxReaderService(string server, string database, string user, string password)
    {
        _server = server;
        _database = database;
        _user = user;
        _password = password;
    }

    private T RunOnStaThread<T>(Func<T> func, TimeSpan timeout)
    {
        Exception? captured = null;
        T result = default!;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(timeout))
        {
            thread.Interrupt();
            throw new TimeoutException($"Appel COM Sage sans réponse en {timeout.TotalSeconds}s. Le document est peut-être ouvert dans Sage.");
        }
        if (captured != null) throw captured;
        return result;
    }

    public void VerifierDisponibilite()
    {
        RunOnStaThread(() =>
        {
            var app = new BSCIALApplication100c();
            try
            {
                OpenSession(app);
            }
            catch (COMException comEx)
            {
                throw new Exception($"Base indisponible : {comEx.Message}", comEx);
            }
            finally
            {
                CloseAndRelease(app);
            }
            return true;
        }, TimeSpan.FromSeconds(15));
    }

    private void OpenSession(BSCIALApplication100c app)
    {
        app.Name = "";
        app.CompanyServer = _server;
        app.CompanyDatabaseName = _database;
        app.Loggable.UserName = _user;
        app.Loggable.UserPwd = _password;
        app.Open();
    }

    private void CloseAndRelease(BSCIALApplication100c app)
    {
        try { if (app.IsOpen) app.Close(); } catch { }
        try { Marshal.ReleaseComObject(app); } catch { }
    }

    public DeviseSocieteInfo ObtenirDeviseSociete(IBSCPTAApplication3 cptaApp)
    {
        var list = cptaApp.FactoryDossier.List;
        if (list.Count > 0)
        {
            var dossier = (IBPDossier2)list[1];
            try
            {
                var devise = dossier.DeviseCompte;
                if (devise != null)
                {
                    try
                    {
                        string format = devise.D_Format;
                        string intitule = devise.D_Intitule;
                        int dec = 2; // default
                        
                        if (!string.IsNullOrEmpty(format))
                        {
                            int dotPos = format.LastIndexOf('.');
                            int commaPos = format.LastIndexOf(',');
                            int lastSep = Math.Max(dotPos, commaPos);
                            
                            if (lastSep != -1)
                            {
                                string afterSep = format.Substring(lastSep + 1);
                                if (!afterSep.Contains("#") && afterSep.Length <= 4)
                                {
                                    dec = afterSep.Length;
                                }
                                else
                                {
                                    dec = 0;
                                }
                            }
                            else
                            {
                                dec = 0;
                            }
                        }
                        return new DeviseSocieteInfo { Intitule = intitule, Decimales = dec };
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(devise);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dossier);
            }
        }
        return new DeviseSocieteInfo { Intitule = "Inconnue", Decimales = 2 };
    }


    public DocumentTaxesInfo LireFactureVente(string numeroPiece)
    {
        return RunOnStaThread(() =>
        {
            var app = new BSCIALApplication100c();
            try
            {
                OpenSession(app);
                var docFactory = app.FactoryDocumentVente;
                
                DocumentType docTypeToUse = DocumentType.DocumentTypeVenteFacture;
                if (!docFactory.ExistPiece(docTypeToUse, numeroPiece))
                {
                    docTypeToUse = (DocumentType)7; // DocumentTypeVenteFactureComptabilisee
                    if (!docFactory.ExistPiece(docTypeToUse, numeroPiece))
                    {
                        throw new Exception($"Facture de vente {numeroPiece} introuvable (ni en Facture, ni en Facture Comptabilisée).");
                    }
                }

                var doc = (IBODocumentVente3)docFactory.ReadPiece(docTypeToUse, numeroPiece);
                try
                {
                    var deviseInfo = ObtenirDeviseSociete(app.CptaApplication);
                    return ExtraireTaxes(doc.Valorisation, "Vente", doc, app.CptaApplication, deviseInfo.Decimales);
                }
                finally
                {
                    Marshal.ReleaseComObject(doc);
                }
            }
            finally
            {
                CloseAndRelease(app);
            }
        }, TimeSpan.FromSeconds(30));
    }

    public DocumentTaxesInfo LireFactureAchat(string numeroPiece)
    {
        return RunOnStaThread(() =>
        {
            var app = new BSCIALApplication100c();
            try
            {
                OpenSession(app);
                var docFactory = app.FactoryDocumentAchat;
                
                DocumentType docTypeToUse = DocumentType.DocumentTypeAchatFacture;
                if (!docFactory.ExistPiece(docTypeToUse, numeroPiece))
                {
                    docTypeToUse = (DocumentType)17; // DocumentTypeAchatFactureComptabilisee
                    if (!docFactory.ExistPiece(docTypeToUse, numeroPiece))
                    {
                        throw new Exception($"Facture d'achat {numeroPiece} introuvable (ni en Facture, ni en Facture Comptabilisée).");
                    }
                }

                var doc = (IBODocumentAchat3)docFactory.ReadPiece(docTypeToUse, numeroPiece);
                try
                {
                    var deviseInfo = ObtenirDeviseSociete(app.CptaApplication);
                    return ExtraireTaxes(doc.Valorisation, "Achat", doc, app.CptaApplication, deviseInfo.Decimales);
                }
                finally
                {
                    Marshal.ReleaseComObject(doc);
                }
            }
            finally
            {
                CloseAndRelease(app);
            }
        }, TimeSpan.FromSeconds(30));
    }

    private DocumentTaxesInfo ExtraireTaxes(IDocValorisation valo, string sens, IBODocument3 doc, IBSCPTAApplication3 cptaApp, int decimales, Dictionary<string, string>? cacheTypesTaxes = null)
    {
        double escompte = 0, frais = 0, acompte = 0;
        int realDoType = 0;
        double htLignes = doc.DO_TotalHT;
        
        using (var conn = new System.Data.SqlClient.SqlConnection($"Server={_server};Database={_database};Integrated Security=True;TrustServerCertificate=True"))
        {
            try {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DO_ValFrais, DO_Escompte, DO_TxEscompte, DO_MontantRegle, DO_Type FROM F_DOCENTETE WHERE DO_Piece = @piece";
                    cmd.Parameters.AddWithValue("@piece", doc.DO_Piece);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            frais = reader.IsDBNull(0) ? 0 : Convert.ToDouble(reader.GetValue(0));
                            short doEscompte = reader.IsDBNull(1) ? (short)0 : Convert.ToInt16(reader.GetValue(1));
                            double txEscompte = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                            acompte = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3));
                            realDoType = reader.IsDBNull(4) ? 0 : Convert.ToInt16(reader.GetValue(4));
                            
                            if (doEscompte == 0 && txEscompte > 0) 
                            {
                                // Percentage on HT + Frais
                                escompte = Math.Round((htLignes + frais) * (txEscompte / 100.0), decimales, MidpointRounding.AwayFromZero);
                            } 
                            else if (doEscompte == 1) 
                            {
                                // Value
                                escompte = txEscompte;
                            }
                        }
                    }
                }
            } catch (Exception ex) { Console.WriteLine("SQL Error: " + ex.Message); }
        }

        var result = new DocumentTaxesInfo
        {
            NumeroPiece = doc.DO_Piece,
            TypeDocument = realDoType,
            Sens = sens,
            TotalHT = Math.Round(htLignes + frais, decimales, MidpointRounding.AwayFromZero), // Total HT Document incl. Frais
            TotalTtc = doc.DO_TotalTTC,
            Escompte = escompte,
            Frais = frais,
            Acompte = acompte
        };

        result.TotalHTNet = Math.Round(result.TotalHT - result.Escompte, decimales, MidpointRounding.AwayFromZero);

        IDocValoTaxes taxes = valo.Taxes;
        
        try
        {
            double totalTvaCalcule = 0;
            double totalParafiscaleCalcule = 0;

            for (int i = 1; i <= taxes.Count; i++)
            {
                var valoTaxe = taxes[i];
                try
                {
                    var detail = new TaxeDetail
                    {
                        Code = valoTaxe.Code,
                        Taux = valoTaxe.Taux,
                        BaseHT = valoTaxe.BaseCalcul,
                        MontantTva = valoTaxe.Montant,
                        TTC = Math.Round(valoTaxe.BaseCalcul + valoTaxe.Montant, decimales, MidpointRounding.AwayFromZero)
                    };
                    
                    // Fetch details from Tax Factory to identify Para/TTC, etc.
                    string? typeCache = null;
                    if (cacheTypesTaxes != null && cacheTypesTaxes.TryGetValue(valoTaxe.Code, out var cType))
                    {
                        typeCache = cType;
                    }
                    else
                    {
                        if (cptaApp.FactoryTaxe.ExistCode(valoTaxe.Code))
                        {
                            var taxeObj = cptaApp.FactoryTaxe.ReadCode(valoTaxe.Code);
                            try
                            {
                                typeCache = taxeObj.TA_Type.ToString();
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(taxeObj);
                            }
                        }
                        else
                        {
                            typeCache = "Inconnu";
                        }
                        if (cacheTypesTaxes != null) cacheTypesTaxes[valoTaxe.Code] = typeCache;
                    }
                    detail.Type = typeCache;

                    result.LignesTaxe.Add(detail);

                    if (detail.Type.StartsWith("TaxeTypeTVA"))
                    {
                        totalTvaCalcule += detail.MontantTva;
                    }
                    else
                    {
                        totalParafiscaleCalcule += detail.MontantTva;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(valoTaxe);
                }
            }
            
            result.TotalTva = Math.Round(totalTvaCalcule, decimales, MidpointRounding.AwayFromZero);
            result.TotalParafiscale = Math.Round(totalParafiscaleCalcule, decimales, MidpointRounding.AwayFromZero);
            result.EcartArrondi = Math.Round(result.TotalTtc - (result.TotalHTNet + result.TotalTva + result.TotalParafiscale), decimales, MidpointRounding.AwayFromZero);
        }
        finally
        {
            Marshal.ReleaseComObject(taxes);
            Marshal.ReleaseComObject(valo);
        }

        return result;
    }

    public IReadOnlyDictionary<string, DocumentTaxesInfo> LireFactures(IEnumerable<(string piece, string sens)> requetes)
    {
        // We set a large timeout for the whole batch
        // The individual pieces won't freeze indefinitely unless Sage is completely dead
        return RunOnStaThread(() =>
        {
            var result = new Dictionary<string, DocumentTaxesInfo>();
            var app = new BSCIALApplication100c();
            try
            {
                OpenSession(app);
                
                var deviseInfo = ObtenirDeviseSociete(app.CptaApplication);
                var cacheTypesTaxes = new Dictionary<string, string>();
                var docFactoryVente = app.FactoryDocumentVente;
                var docFactoryAchat = app.FactoryDocumentAchat;

                foreach (var req in requetes)
                {
                    try
                    {
                        if (req.sens.Equals("Vente", StringComparison.OrdinalIgnoreCase))
                        {
                            DocumentType docTypeToUse = DocumentType.DocumentTypeVenteFacture;
                            if (!docFactoryVente.ExistPiece(docTypeToUse, req.piece))
                            {
                                docTypeToUse = (DocumentType)7;
                                if (!docFactoryVente.ExistPiece(docTypeToUse, req.piece))
                                    throw new Exception($"Facture de vente {req.piece} introuvable.");
                            }
                            var doc = (IBODocumentVente3)docFactoryVente.ReadPiece(docTypeToUse, req.piece);
                            try
                            {
                                result[req.piece + "_" + req.sens] = ExtraireTaxes(doc.Valorisation, "Vente", doc, app.CptaApplication, deviseInfo.Decimales, cacheTypesTaxes);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(doc);
                            }
                        }
                        else if (req.sens.Equals("Achat", StringComparison.OrdinalIgnoreCase))
                        {
                            DocumentType docTypeToUse = DocumentType.DocumentTypeAchatFacture;
                            if (!docFactoryAchat.ExistPiece(docTypeToUse, req.piece))
                            {
                                docTypeToUse = (DocumentType)17;
                                if (!docFactoryAchat.ExistPiece(docTypeToUse, req.piece))
                                    throw new Exception($"Facture d'achat {req.piece} introuvable.");
                            }
                            var doc = (IBODocumentAchat3)docFactoryAchat.ReadPiece(docTypeToUse, req.piece);
                            try
                            {
                                result[req.piece + "_" + req.sens] = ExtraireTaxes(doc.Valorisation, "Achat", doc, app.CptaApplication, deviseInfo.Decimales, cacheTypesTaxes);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(doc);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Erreur sur la pièce {req.piece} ({req.sens}) : {ex.Message}");
                    }
                }
            }
            finally
            {
                CloseAndRelease(app);
            }
            return result;
        }, TimeSpan.FromSeconds(30 * 1000)); // allow generous time for the batch
    }
}
