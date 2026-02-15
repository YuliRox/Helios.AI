using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace LumiRise.Tests.Services.Mqtt;

public class InterruptionDetectorTests
{
    private readonly ILogger<InterruptionDetector> _logger;
    private readonly Mock<IDimmerStateMonitor> _stateMonitorMock = new();
    private readonly Subject<DimmerState> _stateChangesSubject = new();

    public InterruptionDetectorTests(ITestOutputHelper testOutput)
    {
        _logger = new ErrorFailingLogger<InterruptionDetector>(testOutput.WriteLine);
        _stateMonitorMock
            .Setup(x => x.StateChanges)
            .Returns(_stateChangesSubject.AsObservable());
    }

    [Fact]
    public void DetectsPowerOffInterruption()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = false, BrightnessPercent = 0 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().ContainSingle();
        interruptions[0].Reason.Should().Be(InterruptionReason.ManualPowerOff);
    }

    [Fact]
    public void DetectsBrightnessAdjustmentInterruption()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = true, BrightnessPercent = 75 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().ContainSingle();
        interruptions[0].Reason.Should().Be(InterruptionReason.ManualBrightnessAdjustment);
    }

    [Fact]
    public void IgnoresSmallBrightnessVariations()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = true, BrightnessPercent = 51 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotDetectWhenDisabled()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = false, BrightnessPercent = 0 };

        detector.SetExpectedState(expectedState);
        detector.DisableDetection();

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotDetectWithoutExpectedState()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var actualState = new DimmerState { IsOn = false, BrightnessPercent = 0 };

        detector.EnableDetection();
        // Don't set expected state

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().BeEmpty();
    }

    [Fact]
    public void ClearsExpectedState()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = false, BrightnessPercent = 0 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();
        detector.ClearExpectedState();

        _stateChangesSubject.OnNext(actualState);

        interruptions.Should().BeEmpty();
    }

    [Fact]
    public void PublishesInterruptionWithDetails()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState = new DimmerState { IsOn = true, BrightnessPercent = 80 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();

        _stateChangesSubject.OnNext(actualState);

        var evt = interruptions.Should().ContainSingle().Which;
        evt.ExpectedState.Should().Be(expectedState);
        evt.ActualState.Should().Be(actualState);
        evt.Message.Should().NotBeNull();
    }

    [Fact]
    public void DetectsMultipleInterruptions()
    {
        var detector = new InterruptionDetector(_logger, _stateMonitorMock.Object);

        var interruptions = new List<InterruptionEvent>();
        var subscription = detector.Interruptions.Subscribe(evt => interruptions.Add(evt));

        var expectedState = new DimmerState { IsOn = true, BrightnessPercent = 50 };
        var actualState1 = new DimmerState { IsOn = true, BrightnessPercent = 75 };
        var actualState2 = new DimmerState { IsOn = false, BrightnessPercent = 0 };

        detector.SetExpectedState(expectedState);
        detector.EnableDetection();

        _stateChangesSubject.OnNext(actualState1);
        _stateChangesSubject.OnNext(actualState2);

        interruptions.Should().HaveCount(2);
    }
}
