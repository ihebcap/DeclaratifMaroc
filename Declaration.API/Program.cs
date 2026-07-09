using System.Text;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Infrastructure.Factories;
using Declaration.Infrastructure.Repositories;
using Declaration.Application.Services;
using Declaration.Selection;
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

// Configuration JWT
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "default_key_make_it_long_enough";
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

var app = builder.Build();

// Les tables de persistance (DeclarationEntete, LigneCandidate) sont créées
// manuellement dans la base SQL Server GRF via DeclarationTVA.sql.
// L'application ne fait pas de DDL au démarrage.

// Configuration du pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Sert wwwroot (pour le front React/TASK-013)

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
