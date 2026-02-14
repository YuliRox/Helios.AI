using LumiRise.Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace LumiRise.Tests.Services.Mqtt;

/// <summary>
/// Unit tests for MqttConnectionManager.
/// Note: Real connection tests are in integration tests.
/// </summary>
public class MqttConnectionManagerTests(ITestOutputHelper testOutput)
{
    private readonly MqttOptions _options = new();
    private readonly ILogger<LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager> _logger = new ErrorFailingLogger<LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager>(testOutput.WriteLine);

    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        Assert.False(manager.IsConnected);
    }

    [Fact]
    public void ConnectionState_IsObservable()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        // ConnectionState observable should be accessible
        Assert.NotNull(manager.ConnectionState);
    }

    [Fact]
    public void MessageReceived_IsObservable()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        // MessageReceived observable should be accessible
        Assert.NotNull(manager.MessageReceived);
    }

    [Fact]
    public async Task DisposeAsync_NoThrow()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        // Should not throw
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.PublishAsync("test/topic", "payload", CancellationToken.None));
    }

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.SubscribeAsync("test/topic", CancellationToken.None));
    }
}
