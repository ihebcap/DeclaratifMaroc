using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Dapper;
using Declaration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Tresorerie.Dapper;
using Tresorerie.Dapper.Repositories;

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
    /// (tout login + mot de passe "admin"). Réutilise le hashage legacy déjà validé dans GRC_WEB
    /// (Tresorerie.Infrastructure.PasswordHasher contre UT_Hash/UT_Salt) plutôt que le réécrire.
    /// Le JWT porte désormais UT_Id, UT_Admin et les sociétés autorisées (P_SOCUTILISATEUR),
    /// consommés par la garde société (GetAll) et la garde de réouverture (TASK-073).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return Unauthorized(new { Message = "Identifiants invalides." });

        var connectionString = _connectionFactory.GetGrfConnectionString();
        var connectionProvider = new ConnectionProvider { ConnectionString = connectionString };
        var userRepository = new UtilisateurRepository(connectionProvider);

        var user = userRepository.Get(request.Username);
        if (user == null || !user.IsActif)
            return Unauthorized(new { Message = "Identifiants invalides." });

        var hasher = new Tresorerie.Infrastructure.PasswordHasher();
        var computedHash = hasher.Hash(request.Password, user.Salt);
        if (computedHash == null || user.Hash == null || !computedHash.SequenceEqual(user.Hash))
            return Unauthorized(new { Message = "Identifiants invalides." });

        using var connection = _connectionFactory.CreateGrfConnection();
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
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
