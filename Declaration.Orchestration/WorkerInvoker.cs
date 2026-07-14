using System;
using System.Diagnostics;
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
            foreach (var req in requetes)
            {
                reqList.Add(new { NumeroPiece = req.numeroFacture, Sens = req.sens });
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

            // On lance d'abord les lectures async de stdout/stderr pour éviter tout
            // interblocage, puis on écrit stdin et on le ferme (signal de fin d'entrée).
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            process.StandardInput.Write(jsonInput);
            process.StandardInput.Close();

            // Generous timeout for batch
            bool exited = process.WaitForExit(300000);

            if (!exited)
            {
                try { process.Kill(); } catch { }
                throw new Exception($"Timeout lors de l'exécution du worker en mode batch.");
            }

            System.Threading.Tasks.Task.WaitAll(outputTask, errorTask);

            if (process.ExitCode == 0)
            {
                string output = outputTask.Result;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<List<DocumentTaxesInfo>>(output, options) ?? new List<DocumentTaxesInfo>();
            }

            // Batch en échec global : stderr du worker remonté au log (jamais avalé).
            var stderr = errorTask.Result;
            log?.Invoke($"[WORKER BATCH KO] exit={process.ExitCode} : "
                + (string.IsNullOrWhiteSpace(stderr) ? "(stderr vide)" : stderr.Trim()));
            return new List<DocumentTaxesInfo>();
        }
    }
}
