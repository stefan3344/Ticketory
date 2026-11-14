namespace Ticketory.Application.Contracts;

public sealed record PaymentFailedMessage(Guid OrderId, string Reason);
