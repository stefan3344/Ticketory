namespace Ticketory.Api.Options;

public sealed class ExternalToolsOptions
{
    public string GrafanaUrl { get; set; } = "http://localhost:3000";
    public string DozzleUrl { get; set; } = "http://localhost:8888";
    public string RabbitMqUrl { get; set; } = "http://localhost:15672";
    public string PrometheusUrl { get; set; } = "http://localhost:9090";
    public string MailpitUrl { get; set; } = "http://localhost:8025";
}
