namespace Ticketory.Api.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Ticketory";
    public string Audience { get; set; } = "Ticketory.Clients";
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;
    public int ClockSkewSeconds { get; set; } = 30;
}
