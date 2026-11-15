using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Models;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
public class VenuesController(IVenueService venueService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await venueService.GetAllAsync(cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new VenueFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VenueFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await venueService.CreateAsync(
                new CreateVenueRequest(model.Name, model.Address, model.City, model.Capacity),
                cancellationToken);

            TempData["Success"] = "Venue created.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var venue = await venueService.GetByIdAsync(id, cancellationToken);
        if (venue is null)
        {
            return NotFound();
        }

        return View(new VenueFormViewModel
        {
            Id = venue.Id,
            Name = venue.Name,
            Address = venue.Address,
            City = venue.City,
            Capacity = venue.Capacity
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, VenueFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await venueService.UpdateAsync(
                id,
                new UpdateVenueRequest(model.Name, model.Address, model.City, model.Capacity),
                cancellationToken);

            TempData["Success"] = "Venue updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await venueService.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Venue deleted.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
