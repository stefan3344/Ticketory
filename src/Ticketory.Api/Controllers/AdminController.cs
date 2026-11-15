using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Models;
using Ticketory.Application.Abstractions.Services;

namespace Ticketory.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(
    IStatsService statsService,
    IOutboxService outboxService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await statsService.GetDashboardStatsAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Messages(
        string? type,
        bool? processed,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var model = new AdminMessagesViewModel
        {
            Messages = await outboxService.GetMessagesAsync(
                type,
                processed,
                page,
                pageSize,
                cancellationToken),
            Types = await outboxService.GetMessageTypesAsync(cancellationToken),
            Type = type,
            Processed = processed
        };

        return View(model);
    }
}
