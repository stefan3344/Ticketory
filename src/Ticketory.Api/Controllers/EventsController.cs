using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Api.Models;
using Ticketory.Application.Dtos;
using Ticketory.Application.Services;
using Ticketory.Domain.Common;
using Ticketory.Domain.Enums;

namespace Ticketory.Api.Controllers;

public class EventsController(
    EventService eventService,
    VenueService venueService,
    OrderService orderService,
    ReviewService reviewService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var details = await eventService.GetEventDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var userId = isAuthenticated ? User.GetUserId() : Guid.Empty;
        var isAdmin = User.IsInRole("Admin");
        var canManage = isAuthenticated && (isAdmin || details.OrganizerId == userId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var canCancel = canManage &&
                        details.Status is EventStatus.Draft or EventStatus.Published &&
                        (isAdmin || details.StartsAt > now);

        var model = new EventDetailsPageViewModel
        {
            Event = details,
            CanManage = canManage,
            CanCancel = canCancel,
            CanReview = isAuthenticated &&
                        User.IsInRole("Customer") &&
                        await reviewService.CanReviewAsync(id, userId, cancellationToken),
            Buy = new BuyTicketsViewModel
            {
                EventId = details.Id,
                EventTitle = details.Title,
                Items = details.TicketTypes
                    .Select(t => new TicketSelection
                    {
                        TicketTypeId = t.Id,
                        Name = t.Name,
                        Price = t.Price,
                        Available = t.Available
                    })
                    .ToList()
            }
        };

        return View(model);
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> Manage(CancellationToken cancellationToken)
    {
        var events = await eventService.GetManagedEventsAsync(
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return View(events);
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new EventFormViewModel
        {
            Venues = await venueService.GetAllAsync(cancellationToken)
        };

        return View(model);
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Venues = await venueService.GetAllAsync(cancellationToken);
            return View(model);
        }

        try
        {
            var created = await eventService.CreateEventAsync(
                User.GetUserId(),
                new CreateEventRequest(
                    model.Title,
                    model.Description,
                    model.StartsAt,
                    model.EndsAt,
                    model.VenueId,
                    model.ImageUrl),
                cancellationToken);

            TempData["Success"] = "Event created. Add at least one ticket type before publishing.";
            return RedirectToAction("Index", "TicketTypes", new { eventId = created.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Venues = await venueService.GetAllAsync(cancellationToken);
            return View(model);
        }
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var details = await eventService.GetEventDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && details.OrganizerId != User.GetUserId())
        {
            return Forbid();
        }

        var model = new EventFormViewModel
        {
            Id = details.Id,
            Title = details.Title,
            Description = details.Description,
            StartsAt = details.StartsAt.ToLocalTime(),
            EndsAt = details.EndsAt?.ToLocalTime(),
            VenueId = details.VenueId,
            ImageUrl = details.ImageUrl,
            Venues = await venueService.GetAllAsync(cancellationToken)
        };

        return View(model);
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EventFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Venues = await venueService.GetAllAsync(cancellationToken);
            return View(model);
        }

        try
        {
            await eventService.UpdateEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                new UpdateEventRequest(
                    model.Title,
                    model.Description,
                    model.StartsAt,
                    model.EndsAt,
                    model.VenueId,
                    model.ImageUrl),
                cancellationToken);

            TempData["Success"] = "Event updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Venues = await venueService.GetAllAsync(cancellationToken);
            return View(model);
        }
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.DeleteEventAsync(id, User.GetUserId(), User.IsInRole("Admin"), cancellationToken);
            TempData["Success"] = "Event deleted.";
            return RedirectToAction(nameof(Manage));
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.PublishEventAsync(id, User.GetUserId(), User.IsInRole("Admin"), cancellationToken);
            TempData["Success"] = "Event published.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.CancelEventAsync(id, User.GetUserId(), User.IsInRole("Admin"), cancellationToken);
            TempData["Success"] = "Event cancelled.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "OrganizerOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.ReopenEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            TempData["Success"] = "Event reopened as draft.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(Guid id, BuyTicketsViewModel model, CancellationToken cancellationToken)
    {
        var items = model.Items
            .Where(i => i.Quantity > 0)
            .Select(i => new OrderItemRequest(i.TicketTypeId, i.Quantity))
            .ToList();

        if (items.Count == 0)
        {
            TempData["Error"] = "Select at least one ticket.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var order = await orderService.CreateOrderAsync(
                User.GetUserId(),
                new CreateOrderRequest(items),
                model.IdempotencyKey,
                cancellationToken);

            TempData["Success"] = "Order created.";
            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = "CustomerOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(Guid id, int rating, string? comment, CancellationToken cancellationToken)
    {
        try
        {
            await reviewService.AddAsync(
                id,
                User.GetUserId(),
                User.GetDisplayName(),
                new CreateReviewRequest(rating, comment),
                cancellationToken);

            TempData["Success"] = "Thank you for your review.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
