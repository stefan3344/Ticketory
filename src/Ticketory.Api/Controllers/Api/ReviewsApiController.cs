using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/events/{eventId:guid}/reviews")]
public class ReviewsApiController(IReviewService reviewService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> Get(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewService.GetForEventAsync(eventId, cancellationToken));
    }

    [HttpPost]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "CustomerOnly")]
    public async Task<ActionResult<ReviewDto>> Create(
        Guid eventId,
        CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var review = await reviewService.AddAsync(
                eventId,
                User.GetUserId(),
                User.GetDisplayName(),
                request,
                cancellationToken);

            return Created($"/api/events/{eventId}/reviews", review);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot add review.", Detail = ex.Message });
        }
    }
}
