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
                    docTypeToUse = DocumentType.DocumentTypeVenteFactureCpta; // facture comptabilisée
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
                    docTypeToUse = DocumentType.DocumentTypeAchatFactureCpta; // facture comptabilisée
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
                    cmd.CommandText = "SELECT DO_ValFrais, DO_Escompte, DO_TxEscompte, DO_MontantRegle, DO_Type, DO_TotalHTNet FROM F_DOCENTETE WHERE DO_Piece = @piece";
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
                            double htNet = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5));

                            // TASK-046 : sur certaines pièces comptabilisées, l'en-tête Sage a
                            // DO_TotalHT = 0 alors que le HT réel est dans DO_TotalHTNet (= Σ HT lignes,
                            // recoupé DB). On ne fabrique aucune valeur : on remplace la source du HT
                            // document par le champ sibling DO_TotalHTNet quand DO_TotalHT est nul.
                            // Choix DO_TotalHTNet plutôt que l'OM (valo.TotalHT*) : l'OM normalise le HT
                            // en positif (avoirs faussés) et valo.TotalHTBrut vaut 0 sur certaines pièces ;
                            // DO_TotalHTNet conserve la valeur ET le signe attendus par le reste du calcul.
                            // Non-régression : DO_TotalHT ≠ 0 → htLignes inchangé (46/46 pièces justes).
                            if (htLignes == 0 && htNet != 0)
                            {
                                htLignes = htNet;
                            }

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
            // TASK-047 : TTC document sourcé de la valorisation OM (source qui fait foi, comme la TVA),
            // et non plus de l'en-tête SQL DO_TotalTTC. L'OM conserve le signe (avoirs négatifs) et
            // recoupe l'en-tête sur les pièces saines ; sur une pièce à en-tête TTC cassé/nul, l'OM
            // recalcule la valeur. Aucune valeur fabriquée : l'écart réel (ex. -5,74) reste visible.
            TotalTtc = Math.Round(valo.TotalTTC, decimales, MidpointRounding.AwayFromZero),
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

            // TASK-076 : les totaux ci-dessus viennent d'être réellement calculés (lecture Sage
            // aboutie) — ils sont exploitables comme « montants bruts » même si la pièce est
            // ensuite exclue pour incohérence (cf. bloc suivant).
            result.MontantsBrutsDisponibles = true;

            // TASK-072 : incohérence Σ(HT+TVA+Parafiscale) vs TTC document — cf. IncoherenceHtTvaTtc.
            if (IncoherenceHtTvaTtc.EstIncoherent(result.TotalHTNet, result.TotalTva, result.TotalParafiscale, result.TotalTtc, out _))
            {
                result.EnErreur = true;
                result.MotifErreur = $"Incohérence Sage : Σ(HT net+TVA+Parafiscale)={result.TotalHTNet + result.TotalTva + result.TotalParafiscale:F2} "
                    + $"≠ TTC={result.TotalTtc:F2} (écart {result.EcartArrondi:F2}) — pièce {result.NumeroPiece} exclue de la valorisation.";
            }
        }
        finally
        {
            Marshal.ReleaseComObject(taxes);
            Marshal.ReleaseComObject(valo);
        }

        return result;
    }

    private static DocumentTaxesInfo EntreeEnErreur((string piece, string sens) req, string motif)
        => new DocumentTaxesInfo
        {
            NumeroPiece = req.piece,
            Sens = req.sens,
            EnErreur = true,
            MotifErreur = motif
        };

    public IReadOnlyDictionary<string, DocumentTaxesInfo> LireFactures(IEnumerable<(string piece, string sens)> requetes)
    {
        var requests = new List<(string piece, string sens)>(requetes);
        var result = new Dictionary<string, DocumentTaxesInfo>();
        if (requests.Count == 0) return result;

        // Timeouts identiques à la lecture unitaire, mais réappliqués PAR PIÈCE.
        var sessionOpenTimeout = TimeSpan.FromSeconds(30);
        var perPieceTimeout = TimeSpan.FromSeconds(30);

        // La session Sage est ouverte UNE seule fois et tenue vivante sur un unique thread STA
        // dédié qui consomme les pièces une à une depuis une file. Le thread appelant impose le
        // timeout PAR PIÈCE : si une pièce ne rend pas la main (document ouvert dans Sage), elle
        // est isolée en erreur (motif), le thread STA figé est abandonné (IsBackground) et les
        // pièces restantes sont marquées à leur tour — jamais de gel global (~8h), jamais de perte
        // silencieuse. Une pièce introuvable/en exception reste un cas normal, isolé lui aussi.
        var jobs = new System.Collections.Concurrent.BlockingCollection<(string piece, string sens)>(
            new System.Collections.Concurrent.ConcurrentQueue<(string piece, string sens)>());
        var pieceDone = new SemaphoreSlim(0, 1);
        var sessionReady = new SemaphoreSlim(0, 1);
        DocumentTaxesInfo? pieceResult = null;
        Exception? pieceError = null;
        Exception? startupError = null;

        var staThread = new Thread(() =>
        {
            var app = new BSCIALApplication100c();
            try
            {
                OpenSession(app);

                var deviseInfo = ObtenirDeviseSociete(app.CptaApplication);
                var cacheTypesTaxes = new Dictionary<string, string>();
                var docFactoryVente = app.FactoryDocumentVente;
                var docFactoryAchat = app.FactoryDocumentAchat;
                sessionReady.Release();

                foreach (var req in jobs.GetConsumingEnumerable())
                {
                    pieceResult = null;
                    pieceError = null;
                    try
                    {
                        if (req.sens.Equals("Vente", StringComparison.OrdinalIgnoreCase))
                        {
                            DocumentType docTypeToUse = DocumentType.DocumentTypeVenteFacture;
                            if (!docFactoryVente.ExistPiece(docTypeToUse, req.piece))
                            {
                                docTypeToUse = DocumentType.DocumentTypeVenteFactureCpta;
                                if (!docFactoryVente.ExistPiece(docTypeToUse, req.piece))
                                    throw new Exception($"Facture de vente {req.piece} introuvable.");
                            }
                            var doc = (IBODocumentVente3)docFactoryVente.ReadPiece(docTypeToUse, req.piece);
                            try
                            {
                                pieceResult = ExtraireTaxes(doc.Valorisation, "Vente", doc, app.CptaApplication, deviseInfo.Decimales, cacheTypesTaxes);
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
                                docTypeToUse = DocumentType.DocumentTypeAchatFactureCpta;
                                if (!docFactoryAchat.ExistPiece(docTypeToUse, req.piece))
                                    throw new Exception($"Facture d'achat {req.piece} introuvable.");
                            }
                            var doc = (IBODocumentAchat3)docFactoryAchat.ReadPiece(docTypeToUse, req.piece);
                            try
                            {
                                pieceResult = ExtraireTaxes(doc.Valorisation, "Achat", doc, app.CptaApplication, deviseInfo.Decimales, cacheTypesTaxes);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(doc);
                            }
                        }
                        else
                        {
                            throw new Exception($"Sens inconnu : {req.sens}");
                        }
                    }
                    catch (Exception ex)
                    {
                        pieceError = ex;
                    }
                    finally
                    {
                        pieceDone.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                startupError = ex;
                sessionReady.Release();
            }
            finally
            {
                CloseAndRelease(app);
            }
        });
        staThread.IsBackground = true;
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();

        if (!sessionReady.Wait(sessionOpenTimeout))
            throw new TimeoutException($"Ouverture de la session Sage sans réponse en {sessionOpenTimeout.TotalSeconds}s.");
        if (startupError != null)
            throw startupError;

        bool lotInterrompu = false;
        string motifInterruption = "";
        foreach (var req in requests)
        {
            string key = req.piece + "_" + req.sens;
            if (lotInterrompu)
            {
                result[key] = EntreeEnErreur(req, $"Lot interrompu : {motifInterruption}");
                continue;
            }

            jobs.Add(req);
            if (!pieceDone.Wait(perPieceTimeout))
            {
                // La pièce n'a pas rendu la main dans le délai : thread STA figé sur cette lecture.
                motifInterruption = $"Timeout {perPieceTimeout.TotalSeconds}s sur la pièce {req.piece} ({req.sens}) — document peut-être ouvert dans Sage.";
                result[key] = EntreeEnErreur(req, motifInterruption);
                lotInterrompu = true;
                continue;
            }

            result[key] = pieceError != null
                ? EntreeEnErreur(req, pieceError.Message)
                : pieceResult!;
        }

        jobs.CompleteAdding();
        return result;
    }
}
