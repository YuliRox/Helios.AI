using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Implementation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
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

        manager.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void ConnectionState_IsObservable()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        // ConnectionState observable should be accessible
        manager.ConnectionState.Should().NotBeNull();
    }

    [Fact]
    public void MessageReceived_IsObservable()
    {
        var manager = new LumiRise.Api.Services.Mqtt.Implementation.MqttConnectionManager(
            _logger, Options.Create(_options));

        // MessageReceived observable should be accessible
        manager.MessageReceived.Should().NotBeNull();
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

        Func<Task> act = () => manager.PublishAsync("test/topic", "payload", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var manager = new MqttConnectionManager(
            _logger, Options.Create(_options));

        Func<Task> act = () => manager.SubscribeAsync("test/topic", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Constructor_SetsClientCredentials_WhenUsernameIsConfigured()
    {
        var manager = new MqttConnectionManager(
            _logger,
            Options.Create(new MqttOptions
            {
                Username = "prod-user",
                Password = "prod-pass"
            }));

        var credentials = GetCredentialsObject(manager);

        credentials.Should().NotBeNull();

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_DoesNotSetClientCredentials_WhenUsernameIsMissing()
    {
        var manager = new MqttConnectionManager(
            _logger,
            Options.Create(new MqttOptions
            {
                Password = "prod-pass"
            }));

        var credentials = GetCredentialsObject(manager);
        credentials.Should().BeNull();

        await manager.DisposeAsync();
    }

    private static object? GetCredentialsObject(MqttConnectionManager manager)
    {
        var clientOptionsField = typeof(MqttConnectionManager).GetField(
            "_clientOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        clientOptionsField.Should().NotBeNull("manager should keep MQTTnet client options for ConnectAsync");

        var clientOptions = clientOptionsField!.GetValue(manager);
        clientOptions.Should().NotBeNull();

        var credentialsProperty = clientOptions!.GetType().GetProperty("Credentials");
        credentialsProperty.Should().NotBeNull("MQTTnet client options should expose credentials when configured");

        return credentialsProperty!.GetValue(clientOptions);
    }
}
