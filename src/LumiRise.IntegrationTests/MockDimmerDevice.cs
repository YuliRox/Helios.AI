using LumiRise.Api.Configuration;
using MQTTnet;

namespace LumiRise.IntegrationTests;

/// <summary>
/// Mock dimmer device with its own independent MQTT connection.
/// Simulates a real dimmer device using bare MQTTnet client (no LumiRise implementation).
/// </summary>
public sealed class MockDimmerDevice : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly string _server;
    private readonly int _port;
    private readonly MqttTopicsOptions _topics;
    private readonly ITestOutputHelper _output;
    private bool _isPoweredOn;
    private int _currentBrightness;

    public MockDimmerDevice(string server, int port, MqttTopicsOptions topics, ITestOutputHelper output)
    {
        _server = server;
        _port = port;
        _topics = topics;
        _output = output;
        _client = new MqttClientFactory().CreateMqttClient();
        _isPoweredOn = false;
        _currentBrightness = 0;
    }

    /// <summary>
    /// Connects to the broker with its own client and subscribes to command topics.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_server, _port)
            .WithClientId("MockDimmerDevice")
            .Build();

        await _client.ConnectAsync(options, ct);
        _output.WriteLine("[MockDimmer] Connected to broker");

        await _client.SubscribeAsync(_topics.DimmerOnOffCommand, cancellationToken: ct);
        await _client.SubscribeAsync(_topics.DimmerPercentageCommand, cancellationToken: ct);
        _output.WriteLine($"[MockDimmer] Subscribed to {_topics.DimmerOnOffCommand} and {_topics.DimmerPercentageCommand}");

        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();
        _output.WriteLine($"[MockDimmer] Received: {topic} = {payload}");

        try
        {
            if (topic == _topics.DimmerOnOffCommand)
            {
                if (payload.Contains("\"ON\"", StringComparison.OrdinalIgnoreCase))
                {
                    _isPoweredOn = true;
                    _currentBrightness = Math.Max(_currentBrightness, 1);
                }
                else if (payload.Contains("\"OFF\"", StringComparison.OrdinalIgnoreCase))
                {
                    _isPoweredOn = false;
                }

                var powerStatus = _isPoweredOn ? "ON" : "OFF";
                await PublishAsync(_topics.DimmerOnOffStatus, powerStatus);
                _output.WriteLine($"[MockDimmer] Power response: {powerStatus}");
            }
            else if (topic == _topics.DimmerPercentageCommand)
            {
                if (int.TryParse(payload, out var brightness))
                {
                    brightness = Math.Clamp(brightness, 0, 100);
                    _currentBrightness = brightness;
                    _isPoweredOn = brightness > 0;
                }

                var stateJson = $"{{\"POWER\":\"{(_isPoweredOn ? "ON" : "OFF")}\",\"Dimmer\":{_currentBrightness}}}";
                await PublishAsync(_topics.DimmerPercentageStatus, stateJson);
                _output.WriteLine($"[MockDimmer] Brightness response: {stateJson}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[MockDimmer] Error: {ex.Message}");
        }
    }

    private async Task PublishAsync(string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();
        await _client.PublishAsync(message);
    }

    /// <summary>
    /// Simulates a user manually turning off the dimmer (e.g., pressing physical switch).
    /// Publishes status update without receiving a command first.
    /// </summary>
    public async Task SimulateManualPowerOffAsync()
    {
        _isPoweredOn = false;
        await PublishAsync(_topics.DimmerOnOffStatus, "OFF");
        _output.WriteLine("[MockDimmer] Manual intervention: Power OFF");
    }

    /// <summary>
    /// Simulates a user manually turning on the dimmer (e.g., pressing physical switch).
    /// Publishes status update without receiving a command first.
    /// </summary>
    public async Task SimulateManualPowerOnAsync()
    {
        _isPoweredOn = true;
        _currentBrightness = Math.Max(_currentBrightness, 1);
        await PublishAsync(_topics.DimmerOnOffStatus, "ON");
        _output.WriteLine("[MockDimmer] Manual intervention: Power ON");
    }

    /// <summary>
    /// Simulates a user manually adjusting brightness (e.g., using physical dimmer knob).
    /// Publishes status update without receiving a command first.
    /// </summary>
    /// <param name="brightness">The brightness percentage (0-100).</param>
    public async Task SimulateManualBrightnessChangeAsync(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        _currentBrightness = brightness;
        _isPoweredOn = brightness > 0;

        var stateJson = $"{{\"POWER\":\"{(_isPoweredOn ? "ON" : "OFF")}\",\"Dimmer\":{_currentBrightness}}}";
        await PublishAsync(_topics.DimmerPercentageStatus, stateJson);
        _output.WriteLine($"[MockDimmer] Manual intervention: Brightness changed to {brightness}%");
    }

    public async ValueTask DisposeAsync()
    {
        _client.ApplicationMessageReceivedAsync -= HandleMessageAsync;
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
        _client.Dispose();
        _output.WriteLine("[MockDimmer] Disconnected and disposed");
    }
}
