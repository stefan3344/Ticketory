using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers;

[Authorize]
public class OrdersController(IOrderService orderService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetMyOrdersAsync(User.GetUserId(), cancellationToken);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(
            id,
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await orderService.CancelOrderAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            TempData["Success"] = "Order cancelled and tickets released.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> All(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetAllOrdersAsync(200, cancellationToken);
        return View(orders);
    }
}
