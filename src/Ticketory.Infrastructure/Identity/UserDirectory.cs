using Microsoft.AspNetCore.Identity;
using Ticketory.Application.Abstractions.Identity;

namespace Ticketory.Infrastructure.Identity;

public sealed class UserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.FullName is { Length: > 0 } fullName ? fullName : user?.UserName;
    }

    public async Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.Email;
    }
}
