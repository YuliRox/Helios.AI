using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Testcontainers.Mosquitto;
using Testcontainers.Xunit;

namespace LumiRise.IntegrationTests.Services.Mqtt;

/// <summary>
/// Integration tests for MQTT service layer components.
/// Each test receives an isolated Mosquitto container instance via ContainerTest.
/// This ensures test independence and prevents cross-test interference.
/// </summary>
public class MqttIntegrationTests : ContainerTest<MosquittoBuilder, MosquittoContainer>
{
    private readonly ITestOutputHelper _testOutput;

    /// <summary>
    /// Initializes the test with xUnit test output helper for logging.
    /// </summary>
    /// <param name="testOutputHelper">xUnit helper for capturing test output.</param>
    public MqttIntegrationTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        _testOutput = testOutputHelper;
    }

    /// <summary>
    /// Creates a logger that writes to xUnit test output for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which the logger is created.</typeparam>
    /// <returns>ILogger instance that outputs to xUnit.</returns>
    private ILogger<T> CreateTestLogger<T>()
        => new ErrorFailingLogger<T>(_testOutput);

    /// <summary>
    /// Configures the Mosquitto container with pinned image version for reproducibility.
    /// </summary>
    /// <returns>Configured MosquittoBuilder instance.</returns>
    protected override MosquittoBuilder Configure()
        => new MosquittoBuilder("eclipse-mosquitto:2.0");

    /// <summary>
    /// Creates pre-configured MQTT options for the current container instance.
    /// </summary>
    /// <returns>MqttOptions configured with container hostname and mapped port.</returns>
    private MqttOptions GetMqttOptions()
        => new()
        {
            Server = Container.Hostname,
            Port = Container.MqttPort,
            ClientId = "LumiRise-Test"
        };

    [Fact]
    public async Task ConnectAsync_ConnectsToRealBroker()
    {
        var options = GetMqttOptions();
        var manager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        await manager.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.True(manager.IsConnected);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task PublishAndSubscribe_ReceivesPublishedMessages()
    {
        var options = GetMqttOptions();
        var manager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var messages = new List<(string Topic, string Payload)>();
        var subscription = manager.MessageReceived.Subscribe(msg => messages.Add(msg));

        await manager.ConnectAsync(TestContext.Current.CancellationToken);
        await manager.SubscribeAsync("test/topic", TestContext.Current.CancellationToken);

        // Give broker time to process subscription
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await manager.PublishAsync("test/topic", "test-payload", TestContext.Current.CancellationToken);

        // Wait for message to be received
        await Task.Delay(200, TestContext.Current.CancellationToken);

        subscription.Dispose();
        await manager.DisposeAsync();

        Assert.NotEmpty(messages);
        Assert.Single(messages);
        Assert.Equal("test/topic", messages[0].Topic);
        Assert.Equal("test-payload", messages[0].Payload);
    }

    [Fact]
    public async Task DimmerStateMonitor_ParsesRealMessages()
    {
        var options = GetMqttOptions();
        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var monitor = new DimmerStateMonitor(
            CreateTestLogger<DimmerStateMonitor>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var states = new List<DimmerState>();
        var subscription = monitor.StateChanges.Subscribe(state => states.Add(state));

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);
        await monitor.StartMonitoringAsync(TestContext.Current.CancellationToken);

        // Give broker time to process subscriptions
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Publish power message
        await connectionManager.PublishAsync(options.Topics.DimmerOnOffStatus, "ON", TestContext.Current.CancellationToken);

        // Wait for message to be processed
        await Task.Delay(200, TestContext.Current.CancellationToken);

        subscription.Dispose();
        await monitor.DisposeAsync();
        await connectionManager.DisposeAsync();

        Assert.NotEmpty(states);
        Assert.True(states[0].IsOn);
    }

    [Fact]
    public async Task BrightnessRamp_CompletesWithoutError()
    {
        var options = GetMqttOptions();

        // Start mock dimmer device with its own independent connection
        await using var mockDimmer = new MockDimmerDevice(
            options.Server, options.Port, options.Topics, _testOutput);
        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);

        // Give mock device time to fully subscribe
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Now connect the LumiRise connection manager
        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(new MqttOptions
            {
                Server = options.Server,
                Port = options.Port,
                RampStepDelayMs = 50
            }));

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);

        var progress = new Progress<int>();
        var progressValues = new List<int>();
        progress.ProgressChanged += (s, v) => progressValues.Add(v);

        // Ramp brightness from 20% to 80%
        await publisher.RampBrightnessAsync(20, 80, TimeSpan.FromMilliseconds(200),
            progress, TestContext.Current.CancellationToken);

        // Give mock device time to process final response
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await connectionManager.DisposeAsync();

        Assert.NotEmpty(progressValues);
        Assert.Equal(80, progressValues.Last());
    }

    [Fact]
    public async Task MultiplePublishers_CoordinateWithMutex()
    {
        var options = GetMqttOptions();

        // Start mock dimmer device with its own independent connection
        await using var mockDimmer = new MockDimmerDevice(
            options.Server, options.Port, options.Topics, _testOutput);
        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);

        // Give mock device time to fully subscribe
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var messages = new List<(string Topic, string Payload)>();
        var subscription = connectionManager.MessageReceived.Subscribe(msg => messages.Add(msg));

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);

        // Subscribe to status topics to receive mock device responses
        await connectionManager.SubscribeAsync(options.Topics.DimmerOnOffStatus, TestContext.Current.CancellationToken);
        await connectionManager.SubscribeAsync(options.Topics.DimmerPercentageStatus, TestContext.Current.CancellationToken);

        // Start multiple concurrent commands
        var task1 = publisher.TurnOnAsync(TestContext.Current.CancellationToken);
        var task2 = publisher.SetBrightnessAsync(50, TestContext.Current.CancellationToken);
        var task3 = publisher.TurnOffAsync(TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2, task3);

        // Give mock device time to process and respond
        await Task.Delay(200, TestContext.Current.CancellationToken);

        subscription.Dispose();
        await connectionManager.DisposeAsync();

        // Should have received responses from mock device
        Assert.NotEmpty(messages);
    }

    [Fact]
    public async Task InterruptionDetection_DetectsManualPowerOff()
    {
        var options = GetMqttOptions();

        // Start mock dimmer device with its own independent connection
        await using var mockDimmer = new MockDimmerDevice(
            options.Server, options.Port, options.Topics, _testOutput);
        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);

        // Give mock device time to fully subscribe
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Set up LumiRise connection and services
        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var monitor = new DimmerStateMonitor(
            CreateTestLogger<DimmerStateMonitor>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var detector = new InterruptionDetector(CreateTestLogger<InterruptionDetector>(), monitor);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);
        await monitor.StartMonitoringAsync(TestContext.Current.CancellationToken);

        // Set brightness to 50% via command (mock device will respond)
        await publisher.SetBrightnessAsync(50, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Now enable interruption detection with expected state
        detector.SetExpectedState(new DimmerState { IsOn = true, BrightnessPercent = 50 });
        detector.EnableDetection();

        // Simulate user manually turning off the light (like pressing physical switch)
        await mockDimmer.SimulateManualPowerOffAsync();

        // Wait for interruption to be detected
        await Task.Delay(200, TestContext.Current.CancellationToken);

        subscription.Dispose();
        await monitor.DisposeAsync();
        await connectionManager.DisposeAsync();

        Assert.NotEmpty(interruptions);
        Assert.Equal(InterruptionReason.ManualPowerOff, interruptions[0].Reason);
    }

    [Fact]
    public async Task InterruptionDetection_DetectsManualBrightnessChange()
    {
        var options = GetMqttOptions();

        // Start mock dimmer device with its own independent connection
        await using var mockDimmer = new MockDimmerDevice(
            options.Server, options.Port, options.Topics, _testOutput);
        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);

        // Give mock device time to fully subscribe
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Set up LumiRise connection and services
        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var monitor = new DimmerStateMonitor(
            CreateTestLogger<DimmerStateMonitor>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var detector = new InterruptionDetector(CreateTestLogger<InterruptionDetector>(), monitor);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);
        await monitor.StartMonitoringAsync(TestContext.Current.CancellationToken);

        // Set brightness to 50% via command (mock device will respond)
        await publisher.SetBrightnessAsync(50, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Now enable interruption detection with expected state
        detector.SetExpectedState(new DimmerState { IsOn = true, BrightnessPercent = 50 });
        detector.EnableDetection();

        // Simulate user manually changing brightness (like using physical dimmer knob)
        await mockDimmer.SimulateManualBrightnessChangeAsync(80);

        // Wait for interruption to be detected
        await Task.Delay(200, TestContext.Current.CancellationToken);

        subscription.Dispose();
        await monitor.DisposeAsync();
        await connectionManager.DisposeAsync();

        Assert.NotEmpty(interruptions);
        Assert.Equal(InterruptionReason.ManualBrightnessAdjustment, interruptions[0].Reason);
    }

    [Fact]
    public async Task BrightnessRamp_CanceledByManualInterruption()
    {
        var options = GetMqttOptions();

        // Start mock dimmer device with its own independent connection
        await using var mockDimmer = new MockDimmerDevice(
            options.Server, options.Port, options.Topics, _testOutput);
        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);

        // Give mock device time to fully subscribe
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Set up LumiRise connection and services
        var connectionManager = new MqttConnectionManager(CreateTestLogger<MqttConnectionManager>(),
            Microsoft.Extensions.Options.Options.Create(options));

        var monitor = new DimmerStateMonitor(
            CreateTestLogger<DimmerStateMonitor>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(options));

        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(),
            connectionManager,
            Microsoft.Extensions.Options.Options.Create(new MqttOptions
            {
                Server = options.Server,
                Port = options.Port,
                RampStepDelayMs = 100  // Slower ramp to allow interruption
            }));

        var detector = new InterruptionDetector(CreateTestLogger<InterruptionDetector>(), monitor);

        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);
        await monitor.StartMonitoringAsync(TestContext.Current.CancellationToken);

        // Track progress and interruptions
        var progressValues = new List<int>();
        var progress = new Progress<int>(v =>
        {
            progressValues.Add(v);
            detector.SetExpectedState(new DimmerState { IsOn = true, BrightnessPercent = v });
            _testOutput.WriteLine($"[Test] Ramp progress: {v}%");
        });

        var interruptions = new List<InterruptionEvent>();
        var interruptionSubscription = detector.Interruptions.Subscribe(evt =>
        {
            interruptions.Add(evt);
            _testOutput.WriteLine($"[Test] Interruption detected: {evt.Reason}");
        });

        // Create cancellation token that will be triggered on interruption
        using var rampCts = new CancellationTokenSource();

        // Cancel ramp when interruption is detected
        var cancelOnInterruptSubscription = detector.Interruptions.Subscribe(_ =>
        {
            _testOutput.WriteLine("[Test] Canceling ramp due to interruption");
            rampCts.Cancel();
        });

        // Set expected state for interruption detection (will be updated as ramp progresses)
        detector.SetExpectedState(new DimmerState { IsOn = true, BrightnessPercent = 20 });
        detector.EnableDetection();

        // Start ramp from 20% to 100% over 2 seconds (should take ~20 steps at 100ms each)
        var rampTask = publisher.RampBrightnessAsync(20, 100, TimeSpan.FromSeconds(2),
            progress, rampCts.Token);

        // Wait for ramp to start and reach ~40-50%
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Simulate user manually turning off the light mid-ramp
        _testOutput.WriteLine("[Test] Simulating manual power off during ramp");
        await mockDimmer.SimulateManualPowerOffAsync();

        // Wait for the ramp task to complete (should be canceled)
        try
        {
            await rampTask;
            _testOutput.WriteLine("[Test] Ramp completed normally (unexpected)");
        }
        catch (OperationCanceledException)
        {
            _testOutput.WriteLine("[Test] Ramp was canceled as expected");
        }

        // Give time for all messages to be processed
        await Task.Delay(100, TestContext.Current.CancellationToken);

        cancelOnInterruptSubscription.Dispose();
        interruptionSubscription.Dispose();
        await monitor.DisposeAsync();
        await connectionManager.DisposeAsync();

        // Verify interruption was detected
        Assert.NotEmpty(interruptions);
        Assert.Equal(InterruptionReason.ManualPowerOff, interruptions[0].Reason);

        // Verify ramp did not complete to 100%
        Assert.NotEmpty(progressValues);
        Assert.True(progressValues.Last() < 100,
            $"Ramp should have been interrupted before reaching 100%, but reached {progressValues.Last()}%");

        _testOutput.WriteLine($"[Test] Ramp stopped at {progressValues.Last()}% after {progressValues.Count} steps");
    }
}
