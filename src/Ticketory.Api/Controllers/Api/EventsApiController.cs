using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Application.Services;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/events")]
public class EventsApiController(EventService eventService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<EventListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await eventService.GetPublishedEventsAsync(search, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDetailsDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var details = await eventService.GetEventDetailsAsync(id, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [HttpPost]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<ActionResult<EventDetailsDto>> Create(
        CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await eventService.CreateEventAsync(
                User.GetUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid event.", Detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await eventService.UpdateEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                request,
                cancellationToken);

            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid event.", Detail = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.DeleteEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot delete event.", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.PublishEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot publish event.", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.CancelEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot cancel event.", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "OrganizerOrAdmin")]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await eventService.ReopenEventAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken);

            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot reopen event.", Detail = ex.Message });
        }
    }
}
