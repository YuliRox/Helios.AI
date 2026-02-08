namespace LumiRise.Api.Configuration;

/// <summary>
/// MQTT topic configuration for dimmer device communication.
/// </summary>
public class MqttTopicsOptions
{
    /// <summary>
    /// Gets or sets the topic for dimmer on/off commands.
    /// Default: "cmnd/dimmer/power"
    /// </summary>
    public string DimmerOnOffCommand { get; set; } = "cmnd/dimmer/power";

    /// <summary>
    /// Gets or sets the topic for dimmer on/off status feedback.
    /// Default: "stat/dimmer/POWER"
    /// </summary>
    public string DimmerOnOffStatus { get; set; } = "stat/dimmer/POWER";

    /// <summary>
    /// Gets or sets the topic for dimmer brightness percentage commands.
    /// Default: "cmnd/dimmer/dimmer"
    /// </summary>
    public string DimmerPercentageCommand { get; set; } = "cmnd/dimmer/dimmer";

    /// <summary>
    /// Gets or sets the topic for dimmer brightness percentage status feedback.
    /// Default: "stat/dimmer/RESULT"
    /// </summary>
    public string DimmerPercentageStatus { get; set; } = "stat/dimmer/RESULT";
}
