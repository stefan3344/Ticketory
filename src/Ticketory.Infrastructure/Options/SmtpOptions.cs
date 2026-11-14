namespace Ticketory.Infrastructure.Options;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "no-reply@ticketory.local";
    public bool UseStartTls { get; set; }
}
