namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Credentials for an agent login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>JWT and the signed-in agent's display data.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);

/// <summary>The identity of the authenticated agent.</summary>
public sealed record AuthUserDto(int Id, string FullName, string Email);
