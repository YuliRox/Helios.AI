namespace LumiRise.Api.Configuration;

/// <summary>
/// Configuration options for MQTT connection and behavior.
/// Bind from configuration section "Mqtt".
/// Environment variables: MQTT__SERVER, MQTT__PORT, MQTT__USERNAME, MQTT__PASSWORD, etc.
/// </summary>
public class MqttOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Mqtt";

    /// <summary>
    /// Gets or sets the MQTT broker server address/hostname.
    /// Default: "localhost"
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the MQTT broker port number.
    /// Default: 1883 (standard MQTT port)
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Gets or sets the client ID for MQTT connection.
    /// Default: "LumiRise"
    /// </summary>
    public string ClientId { get; set; } = "LumiRise";

    /// <summary>
    /// Gets or sets the username for MQTT broker authentication.
    /// If empty or null, no authentication is used.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for MQTT broker authentication.
    /// If empty or null, no authentication is used.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the MQTT keep-alive interval in seconds.
    /// Default: 60
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the initial reconnection delay in milliseconds.
    /// Default: 1000 (1 second)
    /// </summary>
    public int ReconnectionDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum reconnection delay in milliseconds.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int MaxReconnectionDelayMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the exponential backoff multiplier for reconnection delays.
    /// Default: 2.0 (delays double with each retry)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts before giving up.
    /// Default: 20
    /// </summary>
    public int MaxReconnectionAttempts { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of commands that can be queued while disconnected.
    /// Commands exceeding this limit are discarded.
    /// Default: 20
    /// </summary>
    public int CommandQueueDepth { get; set; } = 20;

    /// <summary>
    /// Gets or sets the timeout for command publishing operations in milliseconds.
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the timeout for status confirmation in milliseconds.
    /// Default: 3000 (3 seconds)
    /// </summary>
    public int StatusConfirmationTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Gets or sets the minimum brightness percentage (0-100).
    /// Values below this threshold are clamped to 0 (off).
    /// Default: 20 (20%)
    /// </summary>
    public int MinimumBrightnessPercent { get; set; } = 20;

    /// <summary>
    /// Gets or sets the delay between brightness ramp steps in milliseconds.
    /// Default: 100 (updates brightness 10 times per second)
    /// </summary>
    public int RampStepDelayMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the MQTT topic configuration.
    /// </summary>
    public MqttTopicsOptions Topics { get; set; } = new();
}
