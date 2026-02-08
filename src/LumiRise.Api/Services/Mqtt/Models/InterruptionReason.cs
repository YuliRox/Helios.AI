namespace LumiRise.Api.Services.Mqtt.Models;

/// <summary>
/// Describes the reason for an alarm interruption.
/// </summary>
public enum InterruptionReason
{
    /// <summary>
    /// Unknown or other reason.
    /// </summary>
    Unknown,

    /// <summary>
    /// User manually turned on the light.
    /// </summary>
    ManualPowerOn,
    
    /// <summary>
    /// User manually turned off the light.
    /// </summary>
    ManualPowerOff,

    /// <summary>
    /// User manually adjusted brightness (increase or decrease).
    /// </summary>
    ManualBrightnessAdjustment,

    /// <summary>
    /// Device became disconnected from MQTT broker.
    /// </summary>
    DeviceDisconnected,

    /// <summary>
    /// Timeout waiting for status confirmation.
    /// </summary>
    StatusConfirmationTimeout
}
