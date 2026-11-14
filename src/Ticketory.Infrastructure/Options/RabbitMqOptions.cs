namespace Ticketory.Infrastructure.Options;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "ticketory";
    public string Password { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public string ClientName { get; set; } = "ticketory";
    public string Exchange { get; set; } = "ticketory.events";
    public string RetryExchange { get; set; } = "ticketory.retry";
    public string DeadLetterExchange { get; set; } = "ticketory.dlx";
    public string DeadLetterQueue { get; set; } = "ticketory.dead-letter";
    public int PrefetchCount { get; set; } = 5;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 5000;
    public int ReconnectDelaySeconds { get; set; } = 5;
}
