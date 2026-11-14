using FluentAssertions;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;

namespace Ticketory.Tests.Domain;

public class EventReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_WithInvalidRating_Throws(int rating)
    {
        var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var action = () => EventReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            rating,
            "test",
            createdAt);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_TrimsComment()
    {
        var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var review = EventReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            5,
            "  great event  ",
            createdAt);

        review.Rating.Should().Be(5);
        review.Comment.Should().Be("great event");
        review.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Create_WithBlankComment_StoresNull()
    {
        var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var review = EventReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            "   ",
            createdAt);

        review.Comment.Should().BeNull();
    }
}
