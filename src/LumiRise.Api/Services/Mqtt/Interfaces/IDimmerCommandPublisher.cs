namespace LumiRise.Api.Services.Mqtt.Interfaces;

/// <summary>
/// Publishes commands to control the dimmer device.
/// Handles power control, brightness setting, and ramping sequences.
/// </summary>
public interface IDimmerCommandPublisher
{
    /// <summary>
    /// Turns the dimmer on.
    /// Publishes a power-on command to the device.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TurnOnAsync(CancellationToken ct);

    /// <summary>
    /// Turns the dimmer off.
    /// Publishes a power-off command to the device.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TurnOffAsync(CancellationToken ct);

    /// <summary>
    /// Sets the dimmer brightness to a specific percentage.
    /// Values below the minimum brightness threshold are clamped to 0 (off).
    /// </summary>
    /// <param name="percentage">Brightness percentage (0-100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if percentage is outside 0-100 range.</exception>
    Task SetBrightnessAsync(int percentage, CancellationToken ct);

    /// <summary>
    /// Ramps the brightness from a start value to a target value over a specified duration.
    /// Steps occur at regular intervals defined by RampStepDelayMs configuration.
    /// </summary>
    /// <param name="start">Starting brightness percentage (0-100).</param>
    /// <param name="target">Target brightness percentage (0-100).</param>
    /// <param name="duration">Total duration for the ramp.</param>
    /// <param name="progress">Optional progress reporter for brightness updates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if percentages are outside 0-100 range.</exception>
    Task RampBrightnessAsync(int start, int target, TimeSpan duration,
        IProgress<int>? progress = null, CancellationToken ct = default);
}
