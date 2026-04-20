using System.Reactive.Subjects;
using Hangfire;
using Hangfire.PostgreSql;
using LumiRise.Api.Configuration;
using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
using LumiRise.Api.Services.Alarm.Implementation;
using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Testcontainers.PostgreSql;
using Testcontainers.Xunit;

namespace LumiRise.IntegrationTests.Services.Alarm;

public class AlarmExecutionJobCleanupIntegrationTests : ContainerTest<PostgreSqlBuilder, PostgreSqlContainer>
{
    private readonly ITestOutputHelper _output;

    public AlarmExecutionJobCleanupIntegrationTests(ITestOutputHelper output)
        : base(output)
    {
        _output = output;
    }

    protected override PostgreSqlBuilder Configure()
        => new PostgreSqlBuilder("postgres:16-alpine");

    [Fact]
    public async Task ExecuteAsync_WhenExecutionFails_StillCleansUpAndUnsubscribesBrightnessHandler()
    {
        var alarmId = Guid.NewGuid();
        var rampId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        dbContext.RampProfiles.Add(new RampProfileEntity
        {
            Id = rampId,
            Mode = "cleanup-test",
            StartBrightnessPercent = 20,
            TargetBrightnessPercent = 100,
            RampDurationSeconds = 1800,
            FullBrightnessDurationSeconds = 900,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.AlarmSchedules.Add(new AlarmScheduleEntity
        {
            Id = alarmId,
            Name = "Cleanup test alarm",
            Enabled = true,
            CronExpression = "0 7 * * 1",
            TimeZoneId = "UTC",
            RampProfileId = rampId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mqttConnectionManager = new Mock<IMqttConnectionManager>(MockBehavior.Strict);
        var stateMonitor = new Mock<IDimmerStateMonitor>(MockBehavior.Strict);
        var stateMachineFactory = new Mock<IAlarmStateMachineFactory>(MockBehavior.Strict);

        mqttConnectionManager
            .Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mqttConnectionManager
            .Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        stateMonitor
            .Setup(x => x.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        stateMonitor
            .Setup(x => x.StopMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        FailingAlarmStateMachine? createdMachine = null;
        stateMachineFactory
            .Setup(x => x.Create(It.IsAny<AlarmDefinition>()))
            .Returns<AlarmDefinition>(definition =>
            {
                createdMachine = new FailingAlarmStateMachine(definition);
                return createdMachine;
            });

        var job = new AlarmExecutionJob(
            dbContext,
            stateMachineFactory.Object,
            mqttConnectionManager.Object,
            stateMonitor.Object,
            Options.Create(new AlarmSettingsOptions { TimeZoneId = "UTC" }),
            new ErrorFailingLogger<AlarmExecutionJob>(_output));

        Func<Task> act = () => job.ExecuteAsync(alarmId, context: null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Synthetic state-machine failure");

        createdMachine.Should().NotBeNull();
        createdMachine!.BrightnessSubscriberCount.Should().Be(0);
        createdMachine.Disposed.Should().BeTrue();

        stateMonitor.Verify(x => x.StartMonitoringAsync(It.IsAny<CancellationToken>()), Times.Once);
        stateMonitor.Verify(x => x.StopMonitoringAsync(It.IsAny<CancellationToken>()), Times.Once);
        mqttConnectionManager.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
        mqttConnectionManager.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private LumiRiseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LumiRiseDbContext>()
            .UseNpgsql(Container.GetConnectionString())
            .Options;
        return new LumiRiseDbContext(options);
    }

    private sealed class FailingAlarmStateMachine : IAlarmStateMachine, IDisposable
    {
        private readonly Subject<AlarmStateTransition> _stateTransitions = new();
        private Action<int>? _brightnessChanged;

        public FailingAlarmStateMachine(AlarmDefinition definition)
        {
            Definition = definition;
        }

        public AlarmDefinition Definition { get; }

        public AlarmState CurrentState { get; private set; } = AlarmState.Idle;

        public IObservable<AlarmStateTransition> StateTransitions => _stateTransitions;

        public event Action<int>? BrightnessChanged
        {
            add => _brightnessChanged += value;
            remove => _brightnessChanged -= value;
        }

        public int BrightnessSubscriberCount => _brightnessChanged?.GetInvocationList().Length ?? 0;

        public bool Disposed { get; private set; }

        public AlarmState Fire(AlarmTrigger trigger, string? message = null)
        {
            CurrentState = (CurrentState, trigger) switch
            {
                (AlarmState.Idle, AlarmTrigger.SchedulerTrigger) => AlarmState.Triggered,
                (AlarmState.Triggered, AlarmTrigger.Start) => AlarmState.Running,
                _ => CurrentState
            };

            _stateTransitions.OnNext(new AlarmStateTransition
            {
                AlarmId = Definition.Id,
                PreviousState = CurrentState,
                NewState = CurrentState,
                Trigger = trigger,
                Message = message
            });
            return CurrentState;
        }

        public bool CanFire(AlarmTrigger trigger) => true;

        public IReadOnlyCollection<AlarmTrigger> GetPermittedTriggers() => [];

        public Task ExecuteAsync(CancellationToken ct = default)
        {
            _brightnessChanged?.Invoke(20);
            _brightnessChanged?.Invoke(21);
            throw new InvalidOperationException("Synthetic state-machine failure");
        }

        public void Dispose()
        {
            Disposed = true;
            _stateTransitions.OnCompleted();
            _stateTransitions.Dispose();
        }
    }
}
