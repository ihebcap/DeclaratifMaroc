namespace Declaration.Orchestration
{
    public class WorkerConfig
    {
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string WorkerExePath { get; set; } = "";

        // TASK-159 : timeout batch OM adaptatif = TimeoutBatchMargeMs + N pièces demandées *
        // TimeoutBatchBudgetParPieceMs (WorkerInvoker.InvoquerWorkerBatch), remplace l'ancienne
        // constante fixe WaitForExit(300000) indépendante du volume (cause de l'incident 361
        // pièces du 23/07/2026).
        //
        // Valeurs mesurées en conditions réelles (23/07/2026, base Sage prod NEW_EMA DISTRIBUTION,
        // worker déployé workers\v10\SageTaxReader.Console.v10.exe, cf. VERIFY/TASK-159_verify.md) :
        // régression sur N=1/5/20/50/100/200/361 pièces réelles → coût marginal mesuré ≈ 324ms/pièce,
        // overhead fixe (ouverture session) ≈ 1,7s. Le rejeu exact du scénario incident (361 pièces
        // réelles) a pris 112,6s dans cette session — mais l'incident réel a dépassé 300s pour ce même
        // volume, donc un coût/pièce réel d'AU MOINS ~831ms sous charge/conditions défavorables
        // (réseau, contention SQL Server réelle, etc., non reproduites ici). Le budget ci-dessous est
        // donc fixé nettement au-dessus des deux mesures (≈3x le taux mesuré, ≈1,2x le plancher
        // implicite de l'incident), pas au taux optimiste observé dans cette session.
        public int TimeoutBatchMargeMs { get; set; } = 60000;
        public int TimeoutBatchBudgetParPieceMs { get; set; } = 1000;
    }
}
