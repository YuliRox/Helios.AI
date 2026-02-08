using LumiRise.Api.Services.Mqtt.Models;

namespace LumiRise.Api.Services.Mqtt.Interfaces;

/// <summary>
/// Manages MQTT broker connection lifecycle and command publishing.
/// Implements exponential backoff reconnection with automatic recovery.
/// </summary>
public interface IMqttConnectionManager : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the connection to the MQTT broker is currently active.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets an observable stream of connection state changes.
    /// </summary>
    IObservable<MqttConnectionState> ConnectionState { get; }

    /// <summary>
    /// Connects to the MQTT broker.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken ct);

    /// <summary>
    /// Disconnects from the MQTT broker.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken ct);

    /// <summary>
    /// Publishes a message to a specified MQTT topic.
    /// </summary>
    /// <param name="topic">The MQTT topic to publish to.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
    /// <exception cref="OperationCanceledException">Thrown if operation times out.</exception>
    Task PublishAsync(string topic, string payload, CancellationToken ct);

    /// <summary>
    /// Subscribes to messages on a specified MQTT topic.
    /// </summary>
    /// <param name="topic">The MQTT topic to subscribe to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SubscribeAsync(string topic, CancellationToken ct);

    /// <summary>
    /// Gets an observable stream of messages received on a subscribed topic.
    /// </summary>
    IObservable<(string Topic, string Payload)> MessageReceived { get; }
}
