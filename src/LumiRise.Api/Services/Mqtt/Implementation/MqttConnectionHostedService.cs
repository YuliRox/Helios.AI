using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LumiRise.Api.Services.Mqtt.Implementation;

public sealed class MqttConnectionHostedService : IHostedService
{
    private readonly ILogger<MqttConnectionHostedService> _logger;
    private readonly IMqttConnectionManager _connectionManager;

    public MqttConnectionHostedService(
        ILogger<MqttConnectionHostedService> logger,
        IMqttConnectionManager connectionManager)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(connectionManager);

        _logger = logger;
        _connectionManager = connectionManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT hosted lifecycle starting");
        await _connectionManager.ConnectAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT hosted lifecycle stopping");
        await _connectionManager.DisconnectAsync(cancellationToken);
    }
}
