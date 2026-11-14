using System.ComponentModel.DataAnnotations;

namespace Ticketory.Application.Dtos;

public sealed record CreateReviewRequest(
    [Range(1, 5)] int Rating,
    [StringLength(2000)] string? Comment);

public sealed record ReviewDto(Guid Id, string CustomerName, int Rating, string? Comment, DateTime CreatedAt);
