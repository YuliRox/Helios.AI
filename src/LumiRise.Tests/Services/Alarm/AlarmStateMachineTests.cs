using System.Reactive.Subjects;
using LumiRise.Api.Services.Alarm.Implementation;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LumiRise.Tests.Services.Alarm;

public class AlarmStateMachineTests : IDisposable
{
    private readonly Mock<IDimmerCommandPublisher> _publisher = new();
    private readonly Mock<IInterruptionDetector> _detector = new();
    private readonly ErrorFailingLogger<AlarmStateMachine> _logger;
    private readonly Subject<InterruptionEvent> _interruptionsSubject = new();
    private readonly AlarmDefinition _definition = new()
    {
        Name = "Test Alarm",
        StartBrightnessPercent = 20,
        TargetBrightnessPercent = 100,
        RampDuration = TimeSpan.FromMilliseconds(50)
    };

    private readonly AlarmStateMachine _sut;

    public AlarmStateMachineTests(ITestOutputHelper testOutput)
    {
        _logger = new ErrorFailingLogger<AlarmStateMachine>(testOutput.WriteLine);
        _detector.Setup(d => d.Interruptions).Returns(_interruptionsSubject);
        _sut = new AlarmStateMachine(_definition, _publisher.Object, _detector.Object, _logger);
    }

    public void Dispose() => _sut.Dispose();

    private void TransitionTo(AlarmState target)
    {
        // Walk the shortest path to the desired state
        var path = target switch
        {
            AlarmState.Idle => Array.Empty<AlarmTrigger>(),
            AlarmState.Triggered => [AlarmTrigger.SchedulerTrigger],
            AlarmState.Running => [AlarmTrigger.SchedulerTrigger, AlarmTrigger.Start],
            AlarmState.Paused => [AlarmTrigger.Pause],
            AlarmState.Completed => [AlarmTrigger.SchedulerTrigger, AlarmTrigger.Start, AlarmTrigger.Complete],
            AlarmState.Interrupted => [AlarmTrigger.SchedulerTrigger, AlarmTrigger.Start, AlarmTrigger.ManualOverride],
            AlarmState.Failed => [AlarmTrigger.SchedulerTrigger, AlarmTrigger.Start, AlarmTrigger.Error],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        foreach (var trigger in path) _sut.Fire(trigger);
    }

    // --- State transition tests ---

    [Fact]
    public void InitialState_IsIdle()
    {
        Assert.Equal(AlarmState.Idle, _sut.CurrentState);
    }

    [Theory]
    [InlineData(AlarmState.Idle, AlarmTrigger.SchedulerTrigger, AlarmState.Triggered)]
    [InlineData(AlarmState.Idle, AlarmTrigger.Pause, AlarmState.Paused)]
    [InlineData(AlarmState.Triggered, AlarmTrigger.Start, AlarmState.Running)]
    [InlineData(AlarmState.Triggered, AlarmTrigger.Cancel, AlarmState.Idle)]
    [InlineData(AlarmState.Running, AlarmTrigger.ManualOverride, AlarmState.Interrupted)]
    [InlineData(AlarmState.Running, AlarmTrigger.Complete, AlarmState.Completed)]
    [InlineData(AlarmState.Running, AlarmTrigger.Error, AlarmState.Failed)]
    [InlineData(AlarmState.Interrupted, AlarmTrigger.Reset, AlarmState.Idle)]
    [InlineData(AlarmState.Completed, AlarmTrigger.Reset, AlarmState.Idle)]
    [InlineData(AlarmState.Failed, AlarmTrigger.Reset, AlarmState.Idle)]
    [InlineData(AlarmState.Paused, AlarmTrigger.Resume, AlarmState.Idle)]
    public void Fire_ValidTransition_MovesToExpectedState(
        AlarmState from, AlarmTrigger trigger, AlarmState expected)
    {
        TransitionTo(from);
        var result = _sut.Fire(trigger);
        Assert.Equal(expected, result);
        Assert.Equal(expected, _sut.CurrentState);
    }

    [Theory]
    [InlineData(AlarmState.Idle, AlarmTrigger.Start)]
    [InlineData(AlarmState.Idle, AlarmTrigger.Complete)]
    [InlineData(AlarmState.Idle, AlarmTrigger.Reset)]
    [InlineData(AlarmState.Running, AlarmTrigger.SchedulerTrigger)]
    [InlineData(AlarmState.Completed, AlarmTrigger.Complete)]
    [InlineData(AlarmState.Paused, AlarmTrigger.Start)]
    public void Fire_InvalidTransition_ThrowsInvalidOperationException(
        AlarmState from, AlarmTrigger trigger)
    {
        TransitionTo(from);
        Assert.Throws<InvalidOperationException>(() => _sut.Fire(trigger));
        Assert.Equal(from, _sut.CurrentState); // state unchanged
    }

    // --- CanFire / GetPermittedTriggers ---

    [Fact]
    public void CanFire_ReflectsTransitionTable()
    {
        Assert.True(_sut.CanFire(AlarmTrigger.SchedulerTrigger));
        Assert.True(_sut.CanFire(AlarmTrigger.Pause));
        Assert.False(_sut.CanFire(AlarmTrigger.Complete));
        Assert.False(_sut.CanFire(AlarmTrigger.Reset));
    }

    [Fact]
    public void GetPermittedTriggers_ReturnsOnlyValidTriggers()
    {
        TransitionTo(AlarmState.Running);
        var permitted = _sut.GetPermittedTriggers();
        Assert.Equal(
            new HashSet<AlarmTrigger> { AlarmTrigger.ManualOverride, AlarmTrigger.Complete, AlarmTrigger.Error },
            new HashSet<AlarmTrigger>(permitted));
    }

    // --- Observable notifications ---

    [Fact]
    public void Fire_PublishesTransitionOnObservable()
    {
        var transitions = new List<AlarmStateTransition>();
        using var sub = _sut.StateTransitions.Subscribe(transitions.Add);

        _sut.Fire(AlarmTrigger.SchedulerTrigger, "test msg");

        Assert.Single(transitions);
        var t = transitions[0];
        Assert.Equal(AlarmState.Idle, t.PreviousState);
        Assert.Equal(AlarmState.Triggered, t.NewState);
        Assert.Equal(AlarmTrigger.SchedulerTrigger, t.Trigger);
        Assert.Equal("test msg", t.Message);
        Assert.Equal(_definition.Id, t.AlarmId);
    }

    // --- Full lifecycle ---

    [Fact]
    public void FullLifecycle_IdleToCompletedAndBack()
    {
        _sut.Fire(AlarmTrigger.SchedulerTrigger);
        _sut.Fire(AlarmTrigger.Start);
        _sut.Fire(AlarmTrigger.Complete);
        Assert.Equal(AlarmState.Completed, _sut.CurrentState);

        _sut.Fire(AlarmTrigger.Reset);
        Assert.Equal(AlarmState.Idle, _sut.CurrentState);
    }

    [Fact]
    public void FullLifecycle_InterruptionAndRecovery()
    {
        _sut.Fire(AlarmTrigger.SchedulerTrigger);
        _sut.Fire(AlarmTrigger.Start);
        _sut.Fire(AlarmTrigger.ManualOverride, "user turned off");
        Assert.Equal(AlarmState.Interrupted, _sut.CurrentState);

        _sut.Fire(AlarmTrigger.Reset);
        Assert.Equal(AlarmState.Idle, _sut.CurrentState);

        // Can restart after reset
        _sut.Fire(AlarmTrigger.SchedulerTrigger);
        Assert.Equal(AlarmState.Triggered, _sut.CurrentState);
    }

    // --- ExecuteAsync ---

    [Fact]
    public async Task ExecuteAsync_WhenNotRunning_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExecuteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulRamp_TransitionsToCompleted()
    {
        TransitionTo(AlarmState.Running);

        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AlarmState.Completed, _sut.CurrentState);
        _publisher.Verify(p => p.TurnOnAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.SetBrightnessAsync(20, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.RampBrightnessAsync(
            20, 100, _definition.RampDuration,
            It.IsAny<IProgress<int>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EnablesAndDisablesInterruptionDetection()
    {
        TransitionTo(AlarmState.Running);

        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        var seq = new MockSequence();
        _detector.Verify(d => d.EnableDetection(), Times.Once);
        _detector.Verify(d => d.DisableDetection(), Times.Once);
        _detector.Verify(d => d.ClearExpectedState(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_TransitionsToFailed()
    {
        using var cts = new CancellationTokenSource();
        _publisher
            .Setup(p => p.TurnOnAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        TransitionTo(AlarmState.Running);
        await _sut.ExecuteAsync(cts.Token);

        Assert.Equal(AlarmState.Failed, _sut.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandThrows_TransitionsToFailed()
    {
        _publisher
            .Setup(p => p.TurnOnAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MQTT disconnected"));

        TransitionTo(AlarmState.Running);
        using (_logger.AllowErrors())
            await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AlarmState.Failed, _sut.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_InterruptionDuringRamp_TransitionsToInterrupted()
    {
        _publisher
            .Setup(p => p.RampBrightnessAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeSpan>(),
                It.IsAny<IProgress<int>?>(), It.IsAny<CancellationToken>()))
            .Returns<int, int, TimeSpan, IProgress<int>?, CancellationToken>((_, _, _, _, _) =>
            {
                // Simulate interruption arriving mid-ramp
                _interruptionsSubject.OnNext(new InterruptionEvent
                {
                    Reason = InterruptionReason.ManualPowerOff,
                    Message = "User turned off"
                });
                return Task.CompletedTask;
            });

        TransitionTo(AlarmState.Running);
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Interruption fires ManualOverride, moving out of Running.
        // The subsequent Complete trigger is ignored (TryFireCore logs warning).
        Assert.Equal(AlarmState.Interrupted, _sut.CurrentState);
    }

    [Fact]
    public async Task ExecuteAsync_CleansUpDetection_EvenOnFailure()
    {
        _publisher
            .Setup(p => p.TurnOnAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        TransitionTo(AlarmState.Running);
        using (_logger.AllowErrors())
            await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        _detector.Verify(d => d.DisableDetection(), Times.Once);
        _detector.Verify(d => d.ClearExpectedState(), Times.Once);
    }

    // --- Dispose safety ---

    [Fact]
    public void Fire_AfterDispose_ThrowsObjectDisposedException()
    {
        _sut.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _sut.Fire(AlarmTrigger.SchedulerTrigger));
    }

    // --- Concurrency ---

    [Fact]
    public async Task Fire_ConcurrentCalls_NoCorruptedState()
    {
        // Rapidly cycle through Idle → Triggered → Idle (via Cancel) from many threads.
        // Validates that _stateLock prevents corruption.
        const int iterations = 1000;
        var tasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
        {
            try
            {
                _sut.Fire(AlarmTrigger.SchedulerTrigger);
                _sut.Fire(AlarmTrigger.Cancel);
            }
            catch (InvalidOperationException)
            {
                // Expected when two threads race on the same transition
            }
        }));

        await Task.WhenAll(tasks);

        // State must be one of the two valid states — never corrupted
        Assert.Contains(_sut.CurrentState, new[] { AlarmState.Idle, AlarmState.Triggered });
    }
}
