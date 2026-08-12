using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Dapper;
using Declaration.Application.Interfaces;
using Declaration.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Declaration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthController(IConfiguration configuration, IDbConnectionFactory connectionFactory)
    {
        _configuration = configuration;
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// TASK-074 : authentification réelle contre P_UTILISATEUR (GRF standard), remplace le mock
    /// (tout login + mot de passe "admin"). TASK-115 : requête Dapper directe (plus de dépendance
    /// binaire à Tresorerie.Dapper.UtilisateurRepository/ConnectionProvider — cette DLL legacy,
    /// figée sur System.Data.SqlClient, obligeait à faire cohabiter deux providers SQL natifs dans
    /// le même publish self-contained aux côtés de Microsoft.Data.SqlClient). Hachage repris à
    /// l'identique (HMACSHA512, même clé) dans <see cref="PasswordHasher"/> pour rester compatible
    /// avec les valeurs UT_HASH/UT_SALT déjà stockées.
    /// Le JWT porte désormais UT_Id, UT_Admin et les sociétés autorisées (P_SOCUTILISATEUR),
    /// consommés par la garde société (GetAll) et la garde de réouverture (TASK-073).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var loginName = !string.IsNullOrWhiteSpace(request.Username) ? request.Username : request.Email;
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { Message = "Identifiants invalides." });

        using var connection = _connectionFactory.CreateGrfConnection();
        var user = connection.QuerySingleOrDefault<UtilisateurRow>(
            @"SELECT UT_Id AS No, UT_Login AS Login, UT_Admin AS IsAdmin, UT_Actif AS IsActif,
                     UT_HASH AS Hash, UT_SALT AS Salt, UT_Nom AS Nom, UT_Prenom AS Prenom
              FROM P_UTILISATEUR WHERE UT_Login = @Login",
            new { Login = loginName });

        if (user == null || !user.IsActif)
            return Unauthorized(new { Message = "Identifiants invalides." });

        var computedHash = PasswordHasher.Hash(request.Password, user.Salt);
        if (computedHash == null || user.Hash == null || !computedHash.SequenceEqual(user.Hash))
            return Unauthorized(new { Message = "Identifiants invalides." });

        var societesAutorisees = user.IsAdmin
            ? Array.Empty<int>()
            : connection.Query<int>("SELECT SO_Id FROM P_SOCUTILISATEUR WHERE UT_Id = @UtId", new { UtId = user.No }).ToArray();

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
            new Claim("UT_Id", user.No.ToString()),
            new Claim("UT_Admin", user.IsAdmin ? "1" : "0"),
            new Claim("Societes", string.Join(",", societesAutorisees))
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtKey = _configuration["JwtSettings:SecretKey"] ?? "default_key_make_it_long_enough";
        var key = Encoding.UTF8.GetBytes(jwtKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(8),
            Issuer = _configuration["JwtSettings:Issuer"],
            Audience = _configuration["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new
        {
            Token = tokenString,
            Username = user.Login,
            Nom = $"{user.Prenom} {user.Nom}".Trim(),
            IsAdmin = user.IsAdmin
        });
    }
}

public class LoginRequest
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string Username { get; set; } = "";

    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string Email { get; set; } = "";

    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string Password { get; set; } = "";
}

/// <summary>TASK-115 : mapping Dapper de P_UTILISATEUR, remplace Tresorerie.Dapper.Models.Utilisateur.</summary>
public class UtilisateurRow
{
    public int No { get; set; }
    public string Login { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool IsActif { get; set; }
    public byte[] Hash { get; set; } = Array.Empty<byte>();
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
}
