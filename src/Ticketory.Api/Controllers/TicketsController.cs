using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Abstractions.Qr;
using Ticketory.Application.Services;

namespace Ticketory.Api.Controllers;

[Authorize]
public class TicketsController(
    OrderService orderService,
    IQrCodeGenerator qrCodeGenerator) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetMyOrdersAsync(User.GetUserId(), cancellationToken);
        var tickets = orders.SelectMany(o => o.Tickets).ToList();
        return View(tickets);
    }

    [HttpGet("/Tickets/Qr/{code}")]
    public async Task<IActionResult> Qr(string code, CancellationToken cancellationToken)
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
