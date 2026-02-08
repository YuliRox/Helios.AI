namespace LumiRise.Api.Services.Mqtt.Models;

/// <summary>
/// Represents an interruption event during alarm execution.
/// </summary>
public class InterruptionEvent
{
    /// <summary>
    /// Gets or sets the reason for the interruption.
    /// </summary>
    public InterruptionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the expected dimmer state before interruption.
    /// </summary>
    public DimmerState? ExpectedState { get; set; }

    /// <summary>
    /// Gets or sets the actual dimmer state when interruption was detected.
    /// </summary>
    public DimmerState? ActualState { get; set; }

    /// <summary>
    /// Gets or sets a detailed message describing the interruption.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the interruption was detected.
    /// </summary>
    public DateTime DetectedAt { get; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"{Reason}: {Message}";
    }
}
