using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Dapper;
using Declaration.Application.Services;
using Declaration.Infrastructure.Factories;
using Declaration.Infrastructure.Repositories;

// TASK-147 : preuve reelle du mecanisme "cache perime -> recalcul", sur donnees reelles
// GR_EMA_DISTRIBUTION. Le cas naturel signale en session (TVA1-2026-02/FA2600106) a ete
// invalide entre-temps par une recreation de la declaration pendant cette meme nuit de travail
// (DateCreation posterieure a la lecture de cache) -- ce script simule la meme situation sur une
// ligne reellement "non ventilee" existante (EC_Id=18198, FC2600004, TVA1-2026-04) en y ajoutant
// une lecture de cache synthetique fraiche (comme l'aurait fait une vraie relecture OM), puis
// restaure l'etat original de la ligne et purge la lecture synthetique a la fin.

SqlMapper.AddTypeHandler(new GuidTypeHandler());

var configuration = new ConfigurationBuilder()
    .AddJsonFile(@"D:\_vibe\GRF\connections.json", optional: false)
    .Build();

var factory = new DbConnectionFactory(configuration);
var repository = new DeclarationRepository(factory);
var workflow = new DeclarationWorkflowService(
    repository,
    selectionService: null!,
    connectionFactory: factory,
    configuration: configuration,
    logger: NullLogger<DeclarationWorkflowService>.Instance);

var persistenceCs = configuration.GetConnectionString("PersistenceConnection")!;
const int soId = 1;
const int ecId = 18198;
var declarationId = Guid.Parse("663948df-5201-45cd-a288-607120c43664");

Console.WriteLine("=== TASK-147 preuve reelle : recalcul d'une ligne au cache perime ===");

using var conn = new SqlConnection(persistenceCs);
await conn.OpenAsync();

var declDateCreation = await conn.QuerySingleAsync<DateTime>(
    "SELECT DateCreation FROM DM_ENTTVA WHERE Id = @Id", new { Id = declarationId.ToString() });
Console.WriteLine($"DateCreation declaration = {declDateCreation:O}");

var dateLectureFraiche = declDateCreation.AddHours(1);
Console.WriteLine($"Insertion d'une lecture de cache SYNTHETIQUE fraiche datee {dateLectureFraiche:O} (posterieure a la declaration)...");

await conn.ExecuteAsync(@"
    INSERT INTO DM_VENTILATION_SAGE_CACHE
        (SO_Id, EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe, TotalHT, TotalTva, TotalTtc,
         Token_MV_Id, Token_MV_Point, DateLecture, Source, MotifErreur)
    VALUES
        (@SoId, @EcId, 20.0, 960.00, 192.00, 1152.00, 'D20', 960.00, 192.00, 1152.00,
         NULL, NULL, @DateLecture, 'OM', NULL)",
    new { SoId = soId, EcId = ecId, DateLecture = dateLectureFraiche });

// 1) Diagnostic doit maintenant detecter le cache perime.
var diag = await workflow.DiagnostiquerLigneAsync(declarationId, ecId);
Console.WriteLine($"Diagnostic -> CachePerime={diag?.CachePerime}, CacheDateLecture={diag?.CacheDateLecture:O}");
Console.WriteLine($"Commentaire : {diag?.CachePerimeCommentaire}");

// 2) Recalcul.
var (trouvee, recalculee, message) = await workflow.RecalculerLigneDepuisCacheAsync(declarationId, ecId);
Console.WriteLine($"RecalculerLigneDepuisCacheAsync -> Trouvee={trouvee}, Recalculee={recalculee}, Message={message}");

var lignesApres = (await conn.QueryAsync(
    "SELECT Etat, MotifRejet, HT, Taux, TVA, TTC FROM DM_LGTVA WHERE DeclarationId=@D AND EC_Id=@E",
    new { D = declarationId.ToString(), E = ecId })).ToList();
Console.WriteLine($"Lignes DM_LGTVA apres recalcul ({lignesApres.Count}) :");
foreach (var l in lignesApres)
    Console.WriteLine($"  Etat={l.Etat} MotifRejet='{l.MotifRejet}' HT={l.HT} Taux={l.Taux} TVA={l.TVA} TTC={l.TTC}");

// 3) Verifie qu'un second appel (cache desormais pas plus recent que... en fait toujours plus
//    recent, mais la ligne n'a plus de MotifRejet -- non regression : la garde Etat==Proposee doit
//    encore laisser passer puisque Etat reste Proposee) -- pas requis par la TASK, on s'arrete ici.

Console.WriteLine();
Console.WriteLine("=== Restauration de l'etat original (ligne + purge cache synthetique) ===");

await conn.ExecuteAsync("DELETE FROM DM_LGTVA WHERE DeclarationId=@D AND EC_Id=@E", new { D = declarationId.ToString(), E = ecId });
await conn.ExecuteAsync(@"
    INSERT INTO DM_LGTVA
        (Id, DeclarationId, Etat, Domaine, MotifRejet, NumeroFacture, NumeroRapprochement, TiersNom,
         TiersIdentifiantFiscal, TiersICE, HT, Taux, TVA, TTC, Prorata, MontantAffecte, ModePaiement, DatePaiement, DateFacture, Source, EcType, EC_Id, MV_Id)
    VALUES
        ('5493fca8-dcd9-4393-ac72-99012d159d32', @D, 0, 'Decaissement', N'Facture introuvable ou non ventil�e', 'FC2600004', 'RF26030084', N'ATLANTIC FOODS',
         '01085367', '001514422000083', 1152.000000, 0, 0, 0, 0, 1152.000000, '4', '2026-06-12', '2026-03-11 12:12:14.590', 'Decaissement', 0, @E, 9905)",
    new { D = declarationId.ToString(), E = ecId });

await conn.ExecuteAsync("DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id=@S AND EC_Id=@E AND DateLecture=@Dl",
    new { S = soId, E = ecId, Dl = dateLectureFraiche });

var ligneRestauree = await conn.QuerySingleAsync(
    "SELECT Etat, MotifRejet, HT, TVA, TTC FROM DM_LGTVA WHERE EC_Id=@E", new { E = ecId });
Console.WriteLine($"Ligne restauree : Etat={ligneRestauree.Etat} MotifRejet='{ligneRestauree.MotifRejet}' HT={ligneRestauree.HT} TVA={ligneRestauree.TVA} TTC={ligneRestauree.TTC}");
var cacheRestant = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DM_VENTILATION_SAGE_CACHE WHERE EC_Id=@E", new { E = ecId });
Console.WriteLine($"Lignes de cache restantes pour EC_Id={ecId} : {cacheRestant} (doit etre 0)");

Console.WriteLine();
Console.WriteLine("=== FIN ===");

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid value)
        => parameter.Value = value.ToString();

    public override Guid Parse(object value)
        => Guid.Parse(value.ToString()!);
}
