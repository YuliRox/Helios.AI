using MQTTnet;

var options = MockDimmerOptions.LoadFromEnvironment();

var shutdownCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdownCts.Cancel();
};

var mqttFactory = new MqttClientFactory();
using var client = mqttFactory.CreateMqttClient();
var stateLock = new object();
var isPoweredOn = false;
var currentBrightness = 0;

client.ApplicationMessageReceivedAsync += async e =>
{
    var topic = e.ApplicationMessage.Topic;
    var payload = e.ApplicationMessage.ConvertPayloadToString();
    Console.WriteLine($"[MockDimmer] Received: {topic} = {payload}");

    if (topic == options.DimmerOnOffCommand)
    {
        lock (stateLock)
        {
            if (payload.Contains("\"ON\"", StringComparison.OrdinalIgnoreCase))
            {
                isPoweredOn = true;
                currentBrightness = Math.Max(currentBrightness, 1);
            }
            else if (payload.Contains("\"OFF\"", StringComparison.OrdinalIgnoreCase))
            {
                isPoweredOn = false;
            }
        }

        var powerStatus = isPoweredOn ? "ON" : "OFF";
        await PublishAsync(client, options.DimmerOnOffStatus, powerStatus, shutdownCts.Token);
        Console.WriteLine($"[MockDimmer] Power response: {powerStatus}");
        return;
    }

    if (topic == options.DimmerPercentageCommand)
    {
        lock (stateLock)
        {
            if (int.TryParse(payload, out var brightness))
            {
                currentBrightness = Math.Clamp(brightness, 0, 100);
                isPoweredOn = currentBrightness > 0;
            }
        }

        var stateJson = BuildStateJson(isPoweredOn, currentBrightness);
        await PublishAsync(client, options.DimmerPercentageStatus, stateJson, shutdownCts.Token);
        Console.WriteLine($"[MockDimmer] Brightness response: {stateJson}");
        return;
    }

    // Manual simulation hook for local testing.
    if (topic == options.ManualPowerTopic)
    {
        lock (stateLock)
        {
            var on = payload.Trim().Equals("ON", StringComparison.OrdinalIgnoreCase);
            isPoweredOn = on;
            if (on)
            {
                currentBrightness = Math.Max(currentBrightness, 1);
            }
        }

        var powerStatus = isPoweredOn ? "ON" : "OFF";
        await PublishAsync(client, options.DimmerOnOffStatus, powerStatus, shutdownCts.Token);
        Console.WriteLine($"[MockDimmer] Manual power event: {powerStatus}");
        return;
    }

    if (topic == options.ManualBrightnessTopic)
    {
        if (int.TryParse(payload, out var brightness))
        {
            lock (stateLock)
            {
                currentBrightness = Math.Clamp(brightness, 0, 100);
                isPoweredOn = currentBrightness > 0;
            }

            var stateJson = BuildStateJson(isPoweredOn, currentBrightness);
            await PublishAsync(client, options.DimmerPercentageStatus, stateJson, shutdownCts.Token);
            Console.WriteLine($"[MockDimmer] Manual brightness event: {stateJson}");
        }
    }
};

Console.WriteLine("[MockDimmer] Starting");
Console.WriteLine($"[MockDimmer] Broker: {options.Server}:{options.Port}");
Console.WriteLine($"[MockDimmer] Command topics: {options.DimmerOnOffCommand}, {options.DimmerPercentageCommand}");
Console.WriteLine($"[MockDimmer] Manual topics: {options.ManualPowerTopic}, {options.ManualBrightnessTopic}");

try
{
    while (!shutdownCts.IsCancellationRequested)
    {
        if (!client.IsConnected)
        {
            try
            {
                var clientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(options.Server, options.Port)
                    .WithClientId(options.ClientId);

                if (!string.IsNullOrWhiteSpace(options.Username))
                {
                    clientOptionsBuilder.WithCredentials(options.Username, options.Password);
                }

                await client.ConnectAsync(clientOptionsBuilder.Build(), shutdownCts.Token);
                Console.WriteLine("[MockDimmer] Connected to MQTT broker");

                await client.SubscribeAsync(options.DimmerOnOffCommand, cancellationToken: shutdownCts.Token);
                await client.SubscribeAsync(options.DimmerPercentageCommand, cancellationToken: shutdownCts.Token);
                await client.SubscribeAsync(options.ManualPowerTopic, cancellationToken: shutdownCts.Token);
                await client.SubscribeAsync(options.ManualBrightnessTopic, cancellationToken: shutdownCts.Token);
                Console.WriteLine("[MockDimmer] Subscriptions registered");
            }
            catch (OperationCanceledException) when (shutdownCts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MockDimmer] Connect/subscribe failed: {ex.Message}");
                await Task.Delay(options.ReconnectDelay, shutdownCts.Token);
                continue;
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(1), shutdownCts.Token);
    }
}
catch (OperationCanceledException)
{
    // Normal shutdown
}
finally
{
    if (client.IsConnected)
    {
        await client.DisconnectAsync();
    }

    Console.WriteLine("[MockDimmer] Stopped");
}

static string BuildStateJson(bool isOn, int brightness)
    => $"{{\"POWER\":\"{(isOn ? "ON" : "OFF")}\",\"Dimmer\":{brightness}}}";

static async Task PublishAsync(IMqttClient client, string topic, string payload, CancellationToken ct)
{
    var message = new MqttApplicationMessageBuilder()
        .WithTopic(topic)
        .WithPayload(payload)
        .Build();
    await client.PublishAsync(message, ct);
}

internal sealed class MockDimmerOptions
{
    public string Server { get; init; } = "mqtt-broker";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = "mock-dimmer-device";
    public string? Username { get; init; }
    public string? Password { get; init; }

    public string DimmerOnOffCommand { get; init; } = "cmnd/dimmer/power";
    public string DimmerOnOffStatus { get; init; } = "stat/dimmer/POWER";
    public string DimmerPercentageCommand { get; init; } = "cmnd/dimmer/dimmer";
    public string DimmerPercentageStatus { get; init; } = "stat/dimmer/RESULT";

    public string ManualPowerTopic { get; init; } = "mock/dimmer/manual/power";
    public string ManualBrightnessTopic { get; init; } = "mock/dimmer/manual/brightness";
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

    public static MockDimmerOptions LoadFromEnvironment()
    {
        static string? Read(string key) => Environment.GetEnvironmentVariable(key);
        static string ReadOrDefault(string key, string fallback) => Read(key) ?? fallback;

        var reconnectDelayMs = 2000;
        if (int.TryParse(Read("MockDimmer__ReconnectDelayMs"), out var parsedDelay) && parsedDelay > 0)
        {
            reconnectDelayMs = parsedDelay;
        }

        var port = 1883;
        if (int.TryParse(Read("MockDimmer__Port"), out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        return new MockDimmerOptions
        {
            Server = ReadOrDefault("MockDimmer__Server", "mqtt-broker"),
            Port = port,
            ClientId = ReadOrDefault("MockDimmer__ClientId", "mock-dimmer-device"),
            Username = Read("MockDimmer__Username"),
            Password = Read("MockDimmer__Password"),
            DimmerOnOffCommand = ReadOrDefault("MockDimmer__DimmerOnOffCommand", "cmnd/dimmer/power"),
            DimmerOnOffStatus = ReadOrDefault("MockDimmer__DimmerOnOffStatus", "stat/dimmer/POWER"),
            DimmerPercentageCommand = ReadOrDefault("MockDimmer__DimmerPercentageCommand", "cmnd/dimmer/dimmer"),
            DimmerPercentageStatus = ReadOrDefault("MockDimmer__DimmerPercentageStatus", "stat/dimmer/RESULT"),
            ManualPowerTopic = ReadOrDefault("MockDimmer__ManualPowerTopic", "mock/dimmer/manual/power"),
            ManualBrightnessTopic = ReadOrDefault("MockDimmer__ManualBrightnessTopic", "mock/dimmer/manual/brightness"),
            ReconnectDelay = TimeSpan.FromMilliseconds(reconnectDelayMs)
        };
    }
}
