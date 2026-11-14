using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Api.Models;
using Ticketory.Application.Dtos;
using Ticketory.Application.Services;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers;

[Authorize(Policy = "OrganizerOrAdmin")]
public class TicketTypesController(
    TicketTypeService ticketTypeService,
    EventService eventService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid eventId, CancellationToken cancellationToken)
    {
        var details = await eventService.GetEventDetailsAsync(eventId, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && details.OrganizerId != User.GetUserId())
        {
            return Forbid();
        }

        ViewBag.EventId = details.Id;
        ViewBag.EventTitle = details.Title;
        return View(details.TicketTypes);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid eventId, CancellationToken cancellationToken)
    {
        var details = await eventService.GetEventDetailsAsync(eventId, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && details.OrganizerId != User.GetUserId())
        {
            return Forbid();
        }

        return View(new TicketTypeFormViewModel
        {
            EventId = details.Id,
            EventTitle = details.Title
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketTypeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await ticketTypeService.CreateAsync(
                User.GetUserId(),
                User.IsInRole("Admin"),
                new CreateTicketTypeRequest(model.EventId, model.Name, model.Price, model.Capacity),
                cancellationToken);

            TempData["Success"] = "Ticket type created.";
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid eventId, CancellationToken cancellationToken)
    {
        var ticketType = await ticketTypeService.GetByIdAsync(id, cancellationToken);
        if (ticketType is null)
        {
            return NotFound();
        }

        var details = await eventService.GetEventDetailsAsync(eventId, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && details.OrganizerId != User.GetUserId())
        {
            return Forbid();
        }

        return View(new TicketTypeFormViewModel
        {
            Id = ticketType.Id,
            EventId = eventId,
            EventTitle = details.Title,
            Name = ticketType.Name,
            Price = ticketType.Price,
            Capacity = ticketType.Capacity
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TicketTypeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await ticketTypeService.UpdateAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                new UpdateTicketTypeRequest(model.Name, model.Price, model.Capacity),
                cancellationToken);

            TempData["Success"] = "Ticket type updated.";
            return RedirectToAction(nameof(Index), new { eventId = model.EventId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            await ticketTypeService.DeleteAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            TempData["Success"] = "Ticket type deleted.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}
