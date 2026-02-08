using LumiRise.Api.Services.Mqtt.Models;

namespace LumiRise.Api.Services.Mqtt.Interfaces;

/// <summary>
/// Monitors the dimmer device state by subscribing to status topics.
/// Parses incoming messages and maintains current state.
/// </summary>
public interface IDimmerStateMonitor : IAsyncDisposable
{
    /// <summary>
    /// Gets the latest cached dimmer state.
    /// Null if no state has been received yet.
    /// </summary>
    DimmerState? CurrentState { get; }

    /// <summary>
    /// Gets an observable stream of dimmer state changes.
    /// </summary>
    IObservable<DimmerState> StateChanges { get; }

    /// <summary>
    /// Starts monitoring the dimmer state.
    /// Subscribes to status topics and begins listening for updates.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartMonitoringAsync(CancellationToken ct);

    /// <summary>
    /// Stops monitoring the dimmer state.
    /// Unsubscribes from status topics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopMonitoringAsync(CancellationToken ct);
}
