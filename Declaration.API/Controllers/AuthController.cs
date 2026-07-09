using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Mock authentication
        if (string.IsNullOrEmpty(request.Username) || request.Password != "admin")
        {
            return Unauthorized(new { Message = "Identifiants invalides (utilisez n'importe quel login avec le mot de passe 'admin')" });
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtKey = _configuration["JwtSettings:SecretKey"] ?? "default_key_make_it_long_enough";
        var key = Encoding.UTF8.GetBytes(jwtKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.Role, "User")
            }),
            Expires = DateTime.UtcNow.AddHours(8),
            Issuer = _configuration["JwtSettings:Issuer"],
            Audience = _configuration["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new { Token = tokenString });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
