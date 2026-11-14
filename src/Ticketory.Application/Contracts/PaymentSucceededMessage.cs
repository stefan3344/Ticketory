namespace Ticketory.Application.Contracts;

public sealed record PaymentSucceededMessage(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    string TransactionReference,
    string CustomerEmail);
