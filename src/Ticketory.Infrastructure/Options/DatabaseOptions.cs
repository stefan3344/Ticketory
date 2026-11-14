namespace Ticketory.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public string Password { get; set; } = string.Empty;
    public bool InitializeOnStartup { get; set; } = true;
}
