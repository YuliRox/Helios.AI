using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace LumiRise.Tests.Services.Mqtt;

public class DimmerStateMonitorTests
{
    private readonly ILogger<DimmerStateMonitor> _logger;
    private readonly Mock<IMqttConnectionManager> _connectionManagerMock = new();
    private readonly MqttOptions _options = new();
    private readonly Subject<(string Topic, string Payload)> _messageSubject = new();

    public DimmerStateMonitorTests(ITestOutputHelper testOutput)
    {
        _logger = new ErrorFailingLogger<DimmerStateMonitor>(testOutput.WriteLine);
        _connectionManagerMock
            .Setup(x => x.MessageReceived)
            .Returns(_messageSubject.AsObservable());
    }

    [Fact]
    public async Task StartMonitoringAsync_SubscribesToBothTopics()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await monitor.StartMonitoringAsync(CancellationToken.None);

        _connectionManagerMock.Verify(
            x => x.SubscribeAsync(_options.Topics.DimmerOnOffStatus, It.IsAny<CancellationToken>()),
            Times.Once);

        _connectionManagerMock.Verify(
            x => x.SubscribeAsync(_options.Topics.DimmerPercentageStatus, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StateChanges_PublishesDimmerStateOnPowerMessage()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        _messageSubject.OnNext((_options.Topics.DimmerOnOffStatus, "ON"));

        Assert.Single(states);
        Assert.True(states[0].IsOn);
    }

    [Fact]
    public async Task StateChanges_HandlesJsonResultMessage()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        _messageSubject.OnNext((_options.Topics.DimmerPercentageStatus, "{\"POWER\":\"ON\",\"Dimmer\":75}"));

        Assert.Single(states);
        Assert.True(states[0].IsOn);
        Assert.Equal(75, states[0].BrightnessPercent);
    }

    [Fact]
    public async Task CurrentState_ReturnsCachedState()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        _messageSubject.OnNext((_options.Topics.DimmerOnOffStatus, "ON"));

        var currentState = monitor.CurrentState;

        Assert.NotNull(currentState);
        Assert.True(currentState.IsOn);
    }

    [Fact]
    public async Task CurrentState_IsNullInitially()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var currentState = monitor.CurrentState;

        Assert.Null(currentState);
    }

    [Fact]
    public async Task StateChanges_OnlyPublishesOnActualChange()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        _messageSubject.OnNext((_options.Topics.DimmerOnOffStatus, "ON"));
        _messageSubject.OnNext((_options.Topics.DimmerOnOffStatus, "ON"));

        Assert.Single(states);
    }

    [Fact]
    public async Task ParsePowerMessage_HandlesMalformedJson()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        _messageSubject.OnNext((_options.Topics.DimmerPercentageStatus, "invalid json"));

        // Should not crash and should not publish state
        Assert.Empty(states);
    }

    [Fact]
    public async Task StopMonitoringAsync_StopsListeningToMessages()
    {
        var monitor = new DimmerStateMonitor(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await monitor.StartMonitoringAsync(CancellationToken.None);
        await monitor.StopMonitoringAsync(CancellationToken.None);

        _messageSubject.OnNext((_options.Topics.DimmerOnOffStatus, "ON"));

        Assert.Empty(states);
    }
}
