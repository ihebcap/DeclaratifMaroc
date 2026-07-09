using System;
using System.Diagnostics;
using System.Text.Json;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration
{
    public interface IWorkerInvoker
    {
        DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config);
    
        List<DocumentTaxesInfo> InvoquerWorkerBatch(IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config);
    }

    public class WorkerInvoker : IWorkerInvoker
    {
        public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config)
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
            
            // Si code de retour ≠ 0, c'est que la facture n'a pas été trouvée ou qu'une autre erreur s'est produite
            // L'orchestrateur s'attend à ce qu'on renvoie null en cas de non-résolution, ce qui produira une alerte.
            return null;
        }
    
        public List<DocumentTaxesInfo> InvoquerWorkerBatch(IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config)
        {
            var reqList = new List<object>();
            foreach (var req in requetes)
            {
                reqList.Add(new { NumeroPiece = req.numeroFacture, Sens = req.sens });
            }
            string jsonInput = JsonSerializer.Serialize(reqList).Replace("\"", "\\\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = config.WorkerExePath,
                Arguments = $"batch \"{jsonInput}\" \"{config.Server}\" \"{config.Database}\" \"{config.User}\" \"{config.Password}\"",
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
            
            return new List<DocumentTaxesInfo>();
        }
    }
}
