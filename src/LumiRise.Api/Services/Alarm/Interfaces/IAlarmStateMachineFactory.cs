using LumiRise.Api.Services.Alarm.Models;

namespace LumiRise.Api.Services.Alarm.Interfaces;

/// <summary>
/// Factory for creating alarm state machine instances.
/// Each alarm gets its own state machine instance.
/// </summary>
public interface IAlarmStateMachineFactory
{
    /// <summary>
    /// Creates a new state machine for the given alarm definition.
    /// </summary>
    /// <param name="definition">The alarm configuration.</param>
    /// <returns>A new alarm state machine instance.</returns>
    IAlarmStateMachine Create(AlarmDefinition definition);
}
