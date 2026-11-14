namespace Ticketory.Application.Abstractions.Identity;

public interface IUserDirectory
{
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default);
}
