using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Alarm.Models;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.Extensions.Logging;

namespace LumiRise.Api.Services.Alarm.Implementation;

/// <summary>
/// Creates alarm state machine instances with injected dependencies.
/// </summary>
public class AlarmStateMachineFactory : IAlarmStateMachineFactory
{
    private readonly IDimmerCommandPublisher _commandPublisher;
    private readonly IInterruptionDetector _interruptionDetector;
    private readonly ILoggerFactory _loggerFactory;

    public AlarmStateMachineFactory(
        IDimmerCommandPublisher commandPublisher,
        IInterruptionDetector interruptionDetector,
        ILoggerFactory loggerFactory)
    {
        _commandPublisher = commandPublisher;
        _interruptionDetector = interruptionDetector;
        _loggerFactory = loggerFactory;
    }

    public IAlarmStateMachine Create(AlarmDefinition definition)
    {
        return new AlarmStateMachine(
            definition,
            _commandPublisher,
            _interruptionDetector,
            _loggerFactory.CreateLogger<AlarmStateMachine>());
    }
}
