using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Services;
using Ticketory.Application.Dtos;
using Ticketory.Infrastructure.Identity;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Conflict(new ProblemDetails { Title = "Email is already registered." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        await userManager.AddToRoleAsync(user, DbInitializer.CustomerRole);
        return Ok(await tokenService.CreateTokenAsync(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails { Title = "Invalid email or password." });
        }

        return Ok(await tokenService.CreateTokenAsync(user));
    }
}
