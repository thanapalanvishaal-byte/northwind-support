using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAgentRepository agents,
    IPasswordHasher<Agent> passwordHasher,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);
        var valid = agent is { IsActive: true, PasswordHash.Length: > 0 } &&
            passwordHasher.VerifyHashedPassword(agent, agent.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
        if (!valid)
            return Unauthorized(new ProblemDetails { Title = "Authentication failed.", Detail = "Invalid email or password.", Status = 401 });

        var now = DateTime.UtcNow;
        var expires = now.AddHours(8);
        var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent!.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, notBefore: now, expires: expires, signingCredentials: credentials);
        return Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, new AuthUserDto(agent.Id, agent.FullName, agent.Email)));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken ct)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var id)) return Unauthorized();
        var agent = await agents.GetByIdAsync(id, ct);
        return agent is null or { IsActive: false } ? Unauthorized() : Ok(new AuthUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
