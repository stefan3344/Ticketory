namespace Ticketory.Application.Contracts;

public static class RoutingKeys
{
    public const string OrderCreated = "order.created";
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentFailed = "payment.failed";
    public const string TicketsIssued = "tickets.issued";
    public const string OrderCancelled = "order.cancelled";
    public const string OrderRefunded = "order.refunded";

    public static string ForMessageType(string type) => type switch
    {
        nameof(OrderCreatedMessage) => OrderCreated,
        nameof(PaymentSucceededMessage) => PaymentSucceeded,
        nameof(PaymentFailedMessage) => PaymentFailed,
        nameof(TicketsIssuedMessage) => TicketsIssued,
        nameof(OrderCancelledMessage) => OrderCancelled,
        nameof(OrderRefundedMessage) => OrderRefunded,
        _ => throw new InvalidOperationException($"Unknown outbox message type '{type}'.")
    };
}
