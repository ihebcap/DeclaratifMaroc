using System.Text;
using Declaration.API.Licence;
using Declaration.Application.Constants;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Infrastructure.Factories;
using Declaration.Infrastructure.Repositories;
using Declaration.Application.Services;
using Declaration.Selection;
using GRLicence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Dapper;

// Enregistrer le TypeHandler Dapper pour mapper Guid <-> colonne NVARCHAR (Id stockés en texte)
SqlMapper.AddTypeHandler(new Declaration.API.GuidTypeHandler());

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};
var builder = WebApplication.CreateBuilder(options);

// Fichier de connexion unique et partagé (copié à côté de l'exe).
// Placé après appsettings.json → il l'écrase.
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "connections.json"),
    optional: true, reloadOnChange: true);

// Hébergement Windows Service
builder.Host.UseWindowsService();

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Injection des dépendances
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IDeclarationRepository, DeclarationRepository>();
// Injection du service réel pour valider les données réelles (GR_EMA_DISTRIBUTION)
builder.Services.AddScoped<ISelectionExpliqueeService, SelectionExpliqueeService>();
builder.Services.AddScoped<DeclarationWorkflowService>();

// TASK-128 : bootstrap Delai de Paiement Maroc (parametre "date de mise en route" par societe +
// reprise manuelle par echeance). Independant du socle TASK-127 (IConventionDelaiPaiementRepository
// / DelaiPaiementService NE SONT PAS enregistres ici -- cable par TASK-129, cf. TASK-127 §Reste a
// valider). DelaiPaiementBootstrapRepository implemente les 2 interfaces (meme pattern que
// DelaiPaiementReferentielRepository pour IJoursReposRepository/IDelaiPaiementParametrageRepository).
builder.Services.AddScoped<IParametrageDelaiPaiementSocieteRepository, DelaiPaiementBootstrapRepository>();
builder.Services.AddScoped<IRepriseDelaiPaiementRepository, DelaiPaiementBootstrapRepository>();
builder.Services.AddScoped<IDelaiPaiementBootstrapService, DelaiPaiementBootstrapService>();

// TASK-117 : verification de licence ApLicence (GRLicence, protocole REQUEST en lecture seule --
// jamais SUBREQ, aucun siege consomme). Le subject est fige en dur (LicenceConstants), jamais lu
// depuis connections.json (anti-contournement : un fichier de config sur le poste client serait
// modifiable par quiconque y a acces disque -- decision PO TASK-002/GRLicence, 17/07/2026).
// Source unique partagee avec Declaration.Setup (TASK-115, complement du 19/07/2026).
builder.Services.AddSingleton<ILicenceConfigProvider, GrfLicenceConfigProvider>();
builder.Services.AddSingleton(sp => new LicenceMonitor(
    LicenceConstants.ApLicenceSubject,
    sp.GetRequiredService<ILicenceConfigProvider>(),
    sp.GetService<ILoggerFactory>()));

// Configuration JWT
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "default_key_make_it_long_enough";

// TASK-114 : la cle par defaut ci-dessus et celle versionnee dans appsettings.json sont
// publiques (committees dans le depot) -- un attaquant qui les connait peut forger un JWT
// valide. On refuse de demarrer en production tant que connections.json n'a pas ete edite
// avec une cle reelle (JwtSettings.SecretKey), plutot que de demarrer silencieusement avec
// une signature connue. ASPNETCORE_ENVIRONMENT vaut "Production" par defaut quand il n'est
// pas positionne (cas du service Windows via sc.exe) -- ce garde-fou s'applique donc sans
// configuration supplementaire.
if (builder.Environment.IsProduction() &&
    (jwtKey == "default_key_make_it_long_enough" ||
     jwtKey == "ThisIsASecretKeyForJwtAuthenticationThatMustBeLongEnough"))
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey est encore la cle de developpement committee dans le depot. " +
        "Definissez une cle reelle et secrete dans connections.json (section JwtSettings.SecretKey) " +
        "avant de lancer l'application en production.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddAuthorization();

// TASK-115 : port d'ecoute parametrable au setup, plutot que fige au defaut Kestrel (5000).
// Lu dans connections.json (ServerConfig.Port), ecrit par Declaration.Setup ; defaut 5000
// conserve pour la retro-compatibilite dev (fichier absent ou section absente).
var port = builder.Configuration.GetValue<int?>("ServerConfig:Port") ?? 5000;
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

// TASK-117 : check initial (bloquant, quelques secondes max selon timeout config) puis demarre le
// recheck periodique fixe (24h, GRLicence). Ne leve jamais d'exception -- l'API demarre toujours,
// licence valide ou non (CDC ApLicence Sec1.3) ; seul l'acces fonctionnel est bloque plus bas par
// le middleware, jamais le process.
var licenceMonitor = app.Services.GetRequiredService<LicenceMonitor>();
await licenceMonitor.DemarrerAsync();

// Les tables de persistance (DeclarationEntete, LigneCandidate) sont créées
// manuellement dans la base SQL Server GRF via DeclarationTVA.sql.
// L'application ne fait pas de DDL au démarrage.

// Configuration du pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TASK-116 : UseDefaultFiles() doit precede UseStaticFiles() pour mapper "/" vers
// wwwroot/index.html -- UseStaticFiles() seul ne sert que les chemins explicites
// (/index.html, /assets/xxx.js...) et renvoyait 404 sur "/" en production.
app.UseDefaultFiles();
app.UseStaticFiles(); // Sert wwwroot (pour le front React/TASK-013)

app.UseCors();

// TASK-117 : point de controle unique (CDC ApLicence Sec1.3) -- bloque tout /api/** si la licence
// n'est pas valide, sauf /api/licence/status lui-meme (sinon le front ne pourrait jamais savoir
// pourquoi il est bloque). Decision de perimetre (point ouvert du TASK) : le front statique
// (wwwroot) n'est JAMAIS bloque ici -- c'est lui qui doit pouvoir charger pour afficher ce message
// et la banniere J-30 (cf. front DeclarationTVA App.tsx). Lecture d'etat en cache uniquement,
// aucun appel reseau synchrone a chaque requete.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/licence/status"))
    {
        var status = licenceMonitor.GetStatus();
        if (!status.EstValide)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = status.Message });
            return;
        }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Filet de securite si le front adopte un jour un routeur cote client (absent aujourd'hui) :
// toute route non API et non fichier statique retombe sur index.html plutot qu'un 404.
app.MapFallbackToFile("index.html");

app.Run();
