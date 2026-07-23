using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration
{
    public interface IWorkerInvoker
    {
        DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null);

        List<DocumentTaxesInfo> InvoquerWorkerBatch(IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config, Action<string>? log = null);
    }

    public class WorkerInvoker : IWorkerInvoker
    {
        public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = config.WorkerExePath,
                Arguments = $"\"{numeroFacture}\" \"{sens}\" \"{config.Server}\" \"{config.Database}\" \"{config.User}\" \"{config.Password}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Impossible de démarrer le process worker.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            // Timeout de 30 secondes
            bool exited = process.WaitForExit(30000);

            if (!exited)
            {
                try { process.Kill(); } catch { }
                throw new Exception($"Timeout lors de l'exécution du worker pour la facture {numeroFacture}.");
            }

            System.Threading.Tasks.Task.WaitAll(outputTask, errorTask);

            if (process.ExitCode == 0)
            {
                string output = outputTask.Result;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<DocumentTaxesInfo>(output, options);
            }

            // Code de retour ≠ 0 : on ne perd JAMAIS l'erreur — stderr du worker remonté au log.
            var stderr = errorTask.Result;
            log?.Invoke($"[WORKER KO] pièce {numeroFacture} ({sens}) exit={process.ExitCode} : "
                + (string.IsNullOrWhiteSpace(stderr) ? "(stderr vide)" : stderr.Trim()));
            return null;
        }
    
        public List<DocumentTaxesInfo> InvoquerWorkerBatch(IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config, Action<string>? log = null)
        {
            var reqList = new List<object>();
            var requetesList = new List<(string numeroFacture, string sens)>();
            foreach (var req in requetes)
            {
                reqList.Add(new { NumeroPiece = req.numeroFacture, Sens = req.sens });
                requetesList.Add(req);
            }
            string jsonInput = JsonSerializer.Serialize(reqList);

            var startInfo = new ProcessStartInfo
            {
                FileName = config.WorkerExePath,
                // La liste JSON passe par stdin (voir StandardInput ci-dessous) et non en
                // argument : sur gros volumes, l'argument dépassait la limite Windows
                // (~32 Ko) et Process.Start échouait avec « Nom de fichier trop long ».
                Arguments = $"batch \"{config.Server}\" \"{config.Database}\" \"{config.User}\" \"{config.Password}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Impossible de démarrer le process worker.");
            }

            // TASK-159 : lecture NDJSON ligne par ligne (au lieu de ReadToEndAsync sur la sortie
            // complète) — chaque ligne reçue est une pièce déjà rendue par le worker (cf.
            // SageTaxReaderService.LireFactures/onPieceCompleted). Ça permet de conserver les
            // pièces déjà lues même si le process est tué faute d'avoir terminé dans le timeout
            // (gros volume), au lieu de tout jeter comme avant.
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resultats = new List<DocumentTaxesInfo>();
            var resultatsLock = new object();
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                try
                {
                    var doc = JsonSerializer.Deserialize<DocumentTaxesInfo>(e.Data, jsonOptions);
                    if (doc != null)
                        lock (resultatsLock) { resultats.Add(doc); }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[WORKER BATCH] ligne de sortie illisible ignorée : {ex.Message}");
                }
            };
            process.BeginOutputReadLine();
            var errorTask = process.StandardError.ReadToEndAsync();

            process.StandardInput.Write(jsonInput);
            process.StandardInput.Close();

            // TASK-159 : timeout adaptatif au volume — remplace l'ancienne constante fixe 300000
            // (indépendante du nombre de pièces, cause de l'incident 361 pièces du 23/07/2026).
            // Formule = marge fixe + N pièces demandées * budget mesuré par pièce (config, cf.
            // WorkerConfig — valeurs par défaut actuelles NON mesurées, voir le commentaire dans
            // WorkerConfig.cs et le VERIFY de TASK-159).
            int timeoutMs = config.TimeoutBatchMargeMs + requetesList.Count * config.TimeoutBatchBudgetParPieceMs;
            bool exited = process.WaitForExit(timeoutMs);

            if (!exited)
            {
                try { process.Kill(); } catch { }
                // Laisse le temps aux derniers OutputDataReceived déjà en vol de se déclencher
                // avant de lire `resultats` (idiome .NET recommandé après un Kill()).
                process.WaitForExit();

                List<DocumentTaxesInfo> partiels;
                lock (resultatsLock) { partiels = new List<DocumentTaxesInfo>(resultats); }

                var piecesRendues = new HashSet<string>(partiels.Select(d => d.NumeroPiece + "_" + d.Sens));
                int manquantes = requetesList.Count(r => !piecesRendues.Contains(r.numeroFacture + "_" + r.sens));
                log?.Invoke($"[WORKER BATCH] timeout après {timeoutMs}ms ({requetesList.Count} pièce(s) demandées) : "
                    + $"{partiels.Count} pièce(s) rendue(s) avant interruption, {manquantes} manquante(s) "
                    + "— repli individuel sur les manquantes uniquement.");
                return partiels;
            }

            // Idiome .NET : laisse les derniers OutputDataReceived se déclencher avant de lire `resultats`.
            process.WaitForExit();
            string stderr = errorTask.GetAwaiter().GetResult();

            lock (resultatsLock)
            {
                if (process.ExitCode == 0)
                    return new List<DocumentTaxesInfo>(resultats);

                // Batch en échec global : stderr du worker remonté au log (jamais avalé). Les
                // pièces déjà rendues avant l'échec sont conservées (rescue partiel), pas jetées.
                log?.Invoke($"[WORKER BATCH KO] exit={process.ExitCode}, {resultats.Count} pièce(s) rendue(s) avant l'échec : "
                    + (string.IsNullOrWhiteSpace(stderr) ? "(stderr vide)" : stderr.Trim()));
                return new List<DocumentTaxesInfo>(resultats);
            }
        }
    }
}
