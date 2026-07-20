using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Declaration.Orchestration.Tests;

// TASK-114 : preuve que la validation JWT (memes TokenValidationParameters que Program.cs)
// rejette un token signe avec la cle de dev committee des lors que l'instance est configuree
// avec une vraie cle de prod, et accepte un token signe/valide avec la meme cle.
public class Task114JwtProdSecretTests
{
    private const string CleDevCommittee = "ThisIsASecretKeyForJwtAuthenticationThatMustBeLongEnough";
    private const string CleProdReelle = "UneVraieCleDeProductionGenereeAleatoirementEtSecrete_2026!";
    private const string Issuer = "DeclarationTvaApi";
    private const string Audience = "DeclarationTvaFront";

    private static string CreerToken(string cleSignature)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cleSignature)),
                SecurityAlgorithms.HmacSha256Signature)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static TokenValidationParameters ParametresValidation(string cleAttendue) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cleAttendue))
    };

    [Fact]
    public void Token_Signe_Avec_Cle_Dev_Du_Depot_Est_Rejete_Par_Une_Instance_Configuree_Avec_La_Cle_Prod()
    {
        var tokenForgeAvecCleDuDepot = CreerToken(CleDevCommittee);
        var handler = new JwtSecurityTokenHandler();

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(tokenForgeAvecCleDuDepot, ParametresValidation(CleProdReelle), out _));
    }

    [Fact]
    public void Token_Signe_Avec_La_Cle_Prod_Est_Accepte_Par_La_Meme_Cle()
    {
        var tokenLegitime = CreerToken(CleProdReelle);
        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(tokenLegitime, ParametresValidation(CleProdReelle), out var validatedToken);

        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
    }
}
