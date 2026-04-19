using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Alarm.Implementation;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Implementation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.Mosquitto;
using Testcontainers.Xunit;

namespace LumiRise.IntegrationTests.Services.Alarm;

public class AlarmExecutionInterruptionIntegrationTests : ContainerTest<MosquittoBuilder, MosquittoContainer>
{
    private readonly ITestOutputHelper _testOutput;

    public AlarmExecutionInterruptionIntegrationTests(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _testOutput = testOutput;
    }

    protected override MosquittoBuilder Configure()
        => new MosquittoBuilder("eclipse-mosquitto:2.0");

    [Fact]
    public async Task ExecuteAsync_WhenDimmerIsManuallyPoweredOff_TransitionsToInterrupted()
    {
        var options = new MqttOptions
        {
            Server = Container.Hostname,
            Port = Container.MqttPort,
            ClientId = "LumiRise-Alarm-Execution-Interrupt-Test",
            RampStepDelayMs = 100
        };

        await using var mockDimmer = new MockDimmerDevice(
            options.Server,
            options.Port,
            options.Topics,
            _testOutput);

        var connectionManager = new MqttConnectionManager(
            CreateTestLogger<MqttConnectionManager>(_testOutput),
            Options.Create(options));
        var monitor = new DimmerStateMonitor(
            CreateTestLogger<DimmerStateMonitor>(_testOutput),
            connectionManager,
            Options.Create(options));
        var detector = new InterruptionDetector(
            CreateTestLogger<InterruptionDetector>(_testOutput),
            monitor);
        var publisher = new DimmerCommandPublisher(
            CreateTestLogger<DimmerCommandPublisher>(_testOutput),
            connectionManager,
            Options.Create(options));
        using var stateMachine = new AlarmStateMachine(
            new AlarmDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Manual interruption integration test alarm",
                Enabled = true,
                StartBrightnessPercent = 20,
                TargetBrightnessPercent = 100,
                RampDuration = TimeSpan.FromSeconds(30),
                TimeZoneId = "UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            publisher,
            detector,
            CreateTestLogger<AlarmStateMachine>(_testOutput));

        var transitions = new List<AlarmStateTransition>();
        using var transitionSubscription = stateMachine.StateTransitions.Subscribe(transitions.Add);

        await mockDimmer.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await connectionManager.ConnectAsync(TestContext.Current.CancellationToken);
        await monitor.StartMonitoringAsync(TestContext.Current.CancellationToken);

        try
        {
            stateMachine.Fire(AlarmTrigger.SchedulerTrigger, "integration test trigger");
            stateMachine.Fire(AlarmTrigger.Start, "integration test start");

            var executeTask = stateMachine.ExecuteAsync(TestContext.Current.CancellationToken);

            // Ensure startup grace window has elapsed before simulating user intervention.
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            await mockDimmer.SimulateManualPowerOffAsync();

            await executeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            stateMachine.CurrentState.Should().Be(AlarmState.Interrupted);
            transitions.Should().Contain(t =>
                t.Trigger == AlarmTrigger.ManualOverride &&
                t.NewState == AlarmState.Interrupted);
            transitions.Should().NotContain(t => t.Trigger == AlarmTrigger.Complete);
        }
        finally
        {
            await monitor.StopMonitoringAsync(TestContext.Current.CancellationToken);
            await connectionManager.DisconnectAsync(TestContext.Current.CancellationToken);
            await monitor.DisposeAsync();
            await connectionManager.DisposeAsync();
            detector.Dispose();
        }
    }

    private static ILogger<T> CreateTestLogger<T>(ITestOutputHelper output)
        => new ErrorFailingLogger<T>(output);
}
