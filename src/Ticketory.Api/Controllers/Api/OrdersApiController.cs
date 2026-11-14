using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketory.Api.Extensions;
using Ticketory.Application.Dtos;
using Ticketory.Application.Services;
using Ticketory.Domain.Common;

namespace Ticketory.Api.Controllers.Api;

[ApiController]
[Route("api/orders")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class OrdersApiController(OrderService orderService) : ControllerBase
{
    [HttpPost]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "CustomerOnly")]
    public async Task<ActionResult<OrderDto>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await orderService.CreateOrderAsync(
                User.GetUserId(),
                request,
                idempotencyKey,
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot create order.", Detail = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetMyOrdersAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(
            id,
            User.GetUserId(),
            User.IsInRole("Admin"),
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await orderService.CancelOrderAsync(
                id,
                User.GetUserId(),
                User.IsInRole("Admin"),
                cancellationToken));
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot cancel order.", Detail = ex.Message });
        }
    }

    [HttpGet("all")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "AdminOnly")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetAllOrdersAsync(200, cancellationToken));
    }
}
