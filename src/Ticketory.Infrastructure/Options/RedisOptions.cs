namespace Ticketory.Infrastructure.Options;

public sealed class RedisOptions
{
    public string Password { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "ticketory:";
    public bool AbortOnConnectFail { get; set; }
    public string DataProtectionKeyName { get; set; } = "ticketory:data-protection-keys";
}
