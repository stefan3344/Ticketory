using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Dtos;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "AdminOnly")]
public class DashboardApiController(IStatsService statsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await statsService.GetDashboardStatsAsync(cancellationToken));
    }
}
