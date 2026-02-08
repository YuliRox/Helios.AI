namespace LumiRise.Api.Services.Mqtt.Models;

/// <summary>
/// Represents the current state of the dimmer device.
/// </summary>
public class DimmerState
{
    /// <summary>
    /// Gets or sets the power state (true = on, false = off).
    /// </summary>
    public bool IsOn { get; set; }

    /// <summary>
    /// Gets or sets the brightness percentage (0-100).
    /// Only meaningful when IsOn is true.
    /// </summary>
    public int BrightnessPercent { get; set; }

    /// <summary>
    /// Gets the timestamp when this state was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; } = DateTime.UtcNow;

    public override string ToString()
    {
        return IsOn
            ? $"On, Brightness: {BrightnessPercent}%"
            : "Off";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not DimmerState other)
            return false;

        return IsOn == other.IsOn && BrightnessPercent == other.BrightnessPercent;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsOn, BrightnessPercent);
    }
}
