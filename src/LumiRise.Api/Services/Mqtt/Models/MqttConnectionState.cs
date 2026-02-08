namespace LumiRise.Api.Services.Mqtt.Models;

/// <summary>
/// Represents the current state of the MQTT broker connection.
/// </summary>
public class MqttConnectionState
{
    /// <summary>
    /// Gets or sets whether the connection is currently active.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the connection attempt number (for tracking retry logic).
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets any error message if the last connection attempt failed.
    /// Null if connection is successful.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets the timestamp when this state was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; } = DateTime.UtcNow;

    public override string ToString()
    {
        return IsConnected
            ? $"Connected (Attempt {AttemptNumber})"
            : $"Disconnected - {LastError ?? "Unknown error"} (Attempt {AttemptNumber})";
    }
}
