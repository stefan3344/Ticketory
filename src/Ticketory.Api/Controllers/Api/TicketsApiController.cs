using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Abstractions.Qr;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Dtos;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/tickets")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TicketsApiController(
    IOrderService orderService,
    IQrCodeGenerator qrCodeGenerator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetMine(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetMyOrdersAsync(User.GetUserId(), cancellationToken);
        return Ok(orders.SelectMany(o => o.Tickets).ToList());
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<TicketDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var ticket = await orderService.GetTicketByCodeAsync(
            code,
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("{code}/qr")]
    [Produces("image/svg+xml")]
    public async Task<IActionResult> GetQr(string code, CancellationToken cancellationToken)
    {
        var ticket = await orderService.GetTicketByCodeAsync(
            code,
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        var svg = qrCodeGenerator.GenerateSvg($"TICKETORY:{ticket.Code}");
        return Content(svg, "image/svg+xml");
    }
}
