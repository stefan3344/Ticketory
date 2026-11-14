using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Application.Dtos;
using Ticketory.Application.Services;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "AdminOnly")]
public class DashboardApiController(StatsService statsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await statsService.GetDashboardStatsAsync(cancellationToken));
    }
}
