using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Ticketory.Infrastructure.Options;

namespace Ticketory.Infrastructure.Messaging;

public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                VirtualHost = settings.VirtualHost,
                UserName = settings.Username,
                Password = settings.Password,
                ClientProvidedName = settings.ClientName,
                AutomaticRecoveryEnabled = true,
                Ssl = new SslOption
                {
                    Enabled = settings.UseTls,
                    ServerName = settings.Host
                }
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", settings.Host, settings.Port);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
