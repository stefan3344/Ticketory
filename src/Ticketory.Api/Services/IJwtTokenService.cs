using Ticketory.Application.Dtos;
using Ticketory.Infrastructure.Identity;

namespace Ticketory.Api.Services;

public interface IJwtTokenService
{
    Task<AuthResponse> CreateTokenAsync(ApplicationUser user);
}
