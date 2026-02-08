using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Manages MQTT broker connection with exponential backoff reconnection strategy.
/// Publishes connection state changes and message events as observables.
/// </summary>
public class MqttConnectionManager : IHostedService, IMqttConnectionManager
{
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly MqttOptions _options;
    private readonly IMqttClient _mqttClient;
    private readonly Subject<MqttConnectionState> _connectionStateSubject = new();
    private readonly Subject<(string Topic, string Payload)> _messageReceivedSubject = new();
    private readonly ConcurrentQueue<(string Topic, string Payload)> _commandQueue = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly object _backgroundTaskLock = new();

    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(10);

    private Task? _backgroundTask;
    private int _connectionAttempt;
    private bool _isConnected;
    private bool _disposed;

    public MqttConnectionManager(
        ILogger<MqttConnectionManager> logger,
        IOptions<MqttOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();
    }

    public bool IsConnected => _isConnected;

    public IObservable<MqttConnectionState> ConnectionState => _connectionStateSubject.AsObservable();

    public IObservable<(string Topic, string Payload)> MessageReceived => _messageReceivedSubject.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT Connection Manager starting");
        await ConnectAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT Connection Manager stopping");
        await DisconnectAsync(cancellationToken);
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_isConnected)
            {
                _logger.LogInformation("Already connected to MQTT broker");
                return;
            }

            _connectionAttempt++;
            var connectionState = new MqttConnectionState
            {
                IsConnected = false,
                AttemptNumber = _connectionAttempt
            };

            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_options.Server, _options.Port)
                    .WithClientId(_options.ClientId)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAliveSeconds));

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    options.WithCredentials(_options.Username, _options.Password);
                }

                var clientOptions = options.Build();

                var result = await _mqttClient.ConnectAsync(clientOptions, ct);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _isConnected = true;
                    connectionState.IsConnected = true;
                    _logger.LogInformation("Connected to MQTT broker at {Server}:{Port}",
                        _options.Server, _options.Port);

                    _connectionStateSubject.OnNext(connectionState);

                    // Hook up message handler for subscribed topics
                    _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceivedAsync;

                    // Reset attempt counter on successful connection
                    _connectionAttempt = 0;

                    // Process any queued commands
                    TrackBackgroundTask(ProcessQueuedCommandsAsync(_disposalCts.Token));
                }
                else
                {
                    _isConnected = false;
                    connectionState.LastError = result.ResultCode.ToString();
                    _logger.LogWarning("Failed to connect to MQTT broker: {ResultCode}",
                        result.ResultCode);
                    _connectionStateSubject.OnNext(connectionState);

                    // Schedule reconnection with exponential backoff
                    TrackBackgroundTask(ScheduleReconnectionAsync(_disposalCts.Token));
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                connectionState.LastError = ex.Message;
                _logger.LogError(ex, "Error connecting to MQTT broker");
                _connectionStateSubject.OnNext(connectionState);

                // Schedule reconnection with exponential backoff
                TrackBackgroundTask(ScheduleReconnectionAsync(_disposalCts.Token));
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (!_isConnected)
                return;

            try
            {
                _mqttClient.ApplicationMessageReceivedAsync -= HandleMessageReceivedAsync;
                await _mqttClient.DisconnectAsync(
                    new MqttClientDisconnectOptions { Reason = MqttClientDisconnectOptionsReason.NormalDisconnection },
                    cancellationToken: ct);
                _logger.LogInformation("Disconnected from MQTT broker");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from MQTT broker");
                throw;
            }
            finally
            {
                _isConnected = false;
                var connectionState = new MqttConnectionState
                {
                    IsConnected = false,
                    AttemptNumber = _connectionAttempt
                };
                _connectionStateSubject.OnNext(connectionState);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_isConnected)
        {
            _logger.LogDebug("Not connected, queueing command: {Topic} = {Payload}", topic, payload);
            _commandQueue.Enqueue((topic, payload));
            throw new InvalidOperationException("MQTT broker is not connected");
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(_options.CommandTimeoutMs));

            await _mqttClient.PublishAsync(message, cts.Token);
            _logger.LogDebug("Published to {Topic}: {Payload}", topic, payload);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout publishing to {Topic}", topic);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to {Topic}", topic);
            throw;
        }
    }

    public async Task SubscribeAsync(string topic, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(topic);

        if (!_isConnected)
        {
            _logger.LogWarning("Not connected, cannot subscribe to {Topic}", topic);
            throw new InvalidOperationException("MQTT broker is not connected");
        }

        try
        {
            await _mqttClient.SubscribeAsync(topic, cancellationToken: ct);
            _logger.LogDebug("Subscribed to {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Tracks a background task so it can be awaited during disposal.
    /// Chains tasks sequentially to avoid unbounded growth.
    /// </summary>
    private void TrackBackgroundTask(Task task)
    {
        lock (_backgroundTaskLock)
        {
            _backgroundTask = _backgroundTask is null
                ? task
                : _backgroundTask.ContinueWith(_ => task, TaskScheduler.Default).Unwrap();
        }
    }

    private async Task ScheduleReconnectionAsync(CancellationToken ct)
    {
        try
        {
            var delayMs = (int)Math.Min(
                _options.ReconnectionDelayMs * Math.Pow(_options.BackoffMultiplier, _connectionAttempt - 1),
                _options.MaxReconnectionDelayMs
            );

            _logger.LogInformation("Scheduling reconnection in {DelayMs}ms (attempt {Attempt})",
                delayMs, _connectionAttempt);

            await Task.Delay(delayMs, ct);
            await ConnectAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Reconnection scheduling cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in reconnection scheduling");
        }
    }

    private async Task ProcessQueuedCommandsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _commandQueue.TryDequeue(out var command))
        {
            try
            {
                _logger.LogInformation("Processing queued command: {Topic} = {Payload}",
                    command.Topic, command.Payload);
                await PublishAsync(command.Topic, command.Payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued command");
                // Re-queue on error
                _commandQueue.Enqueue(command);
                break;
            }
        }
    }

    private Task HandleMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();
        _logger.LogDebug("Received message on {Topic}: {Payload}", topic, payload);
        _messageReceivedSubject.OnNext((topic, payload));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Signal all background tasks to stop
        await _disposalCts.CancelAsync();

        // Wait for tracked background tasks with a timeout
        Task? pending;
        lock (_backgroundTaskLock)
        {
            pending = _backgroundTask;
        }

        if (pending is not null)
        {
            try
            {
                await pending.WaitAsync(DisposalTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Background tasks did not complete within {Timeout}", DisposalTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error awaiting background tasks during disposal");
            }
        }

        // Disconnect with timeout
        try
        {
            using var disconnectCts = new CancellationTokenSource(DisposalTimeout);
            await DisconnectAsync(disconnectCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting during disposal");
        }

        _disposalCts.Dispose();
        _mqttClient.Dispose();

        _connectionStateSubject.OnCompleted();
        _connectionStateSubject.Dispose();

        _messageReceivedSubject.OnCompleted();
        _messageReceivedSubject.Dispose();

        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
