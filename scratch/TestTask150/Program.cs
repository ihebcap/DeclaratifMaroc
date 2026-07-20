using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Declaration.Application.Services;
using Declaration.Infrastructure.Factories;
using Declaration.Infrastructure.Repositories;

// TASK-150 : diagnostic (lecture seule stricte, reutilise DiagnostiquerLigneAsync TASK-144/147,
// aucune ecriture) des 5 lignes confirmees reellement anormales par TASK-149 : 20650, 21473,
// 24098, 18198, 18199.

SqlMapper.AddTypeHandler(new GuidTypeHandler());

var configuration = new ConfigurationBuilder()
    .AddJsonFile(@"D:\_vibe\GRF\connections.json", optional: false)
    .Build();

var factory = new DbConnectionFactory(configuration);
var repository = new DeclarationRepository(factory);
var workflow = new DeclarationWorkflowService(
    repository, selectionService: null!, connectionFactory: factory,
    configuration: configuration, logger: NullLogger<DeclarationWorkflowService>.Instance);

var cas = new (string Numero, Guid DeclarationId, int EcId)[]
{
    ("TVA1-2026-02", Guid.Parse("054a3ed1-00e6-42a0-82ff-fd325bc56ac1"), 20650),
    ("TVA1-2026-02", Guid.Parse("054a3ed1-00e6-42a0-82ff-fd325bc56ac1"), 21473),
    ("TVA1-2026-03", Guid.Parse("f1f365e9-1c3e-4ecc-a19b-33979a857684"), 24098),
    ("TVA1-2026-04", Guid.Parse("663948df-5201-45cd-a288-607120c43664"), 18198),
    ("TVA1-2026-04", Guid.Parse("663948df-5201-45cd-a288-607120c43664"), 18199),
};

foreach (var (numero, declId, ecId) in cas)
{
    Console.WriteLine($"=== {numero} — EC_Id={ecId} ===");
    var diag = await workflow.DiagnostiquerLigneAsync(declId, ecId);
    if (diag == null) { Console.WriteLine("Diagnostic : introuvable."); continue; }
    Console.WriteLine($"Facture : {diag.DoNumero} — Tiers {diag.TiersCode} {diag.TiersIntitule}");
    Console.WriteLine($"Code motif reconnu : {diag.CodeMotifReconnu}");
    Console.WriteLine($"Explication : {diag.ExplicationMetier}");
    Console.WriteLine($"Action recommandee : {diag.ActionRecommandee}");
    Console.WriteLine($"Collision DO_Numero detectee : {diag.CollisionDetectee}");
    if (diag.CollisionDetectee) Console.WriteLine($"  Commentaire collision : {diag.CollisionCommentaire}");
    Console.WriteLine($"Cache perime (TASK-147) : {diag.CachePerime}");
    Console.WriteLine();
}

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid value)
        => parameter.Value = value.ToString();

    public override Guid Parse(object value)
        => Guid.Parse(value.ToString()!);
}
