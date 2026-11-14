using System.ComponentModel.DataAnnotations;

namespace Ticketory.Application.Dtos;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, MinLength(8), StringLength(100)] string Password,
    [Required, StringLength(150, MinimumLength = 2)] string FullName);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(100)] string Password);

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);
