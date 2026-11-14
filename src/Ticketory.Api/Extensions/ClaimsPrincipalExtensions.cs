using System.Security.Claims;

namespace Ticketory.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated user has no valid id claim.");
    }

    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        var fullName = principal.FindFirstValue("full_name");
        return !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity?.Name ?? "User";
    }
}
