using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Exceptions;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Manages MQTT broker connection with Polly-based exponential backoff reconnection.
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
    private readonly ResiliencePipeline _reconnectionPipeline;

    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(10);

    private Task? _reconnectionTask;
    private Task? _queueDrainTask;
    private int _connectionAttempt;
    private int _queuedCommandCount;
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

        _reconnectionPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxReconnectionAttempts,
                DelayGenerator = args =>
                {
                    var delay = TimeSpan.FromMilliseconds(
                        Math.Min(
                            _options.ReconnectionDelayMs * Math.Pow(_options.BackoffMultiplier, args.AttemptNumber),
                            _options.MaxReconnectionDelayMs));
                    return ValueTask.FromResult<TimeSpan?>(delay);
                },
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();
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
        var connected = await ConnectCoreAsync(ct);

        if (!connected)
        {
            StartReconnectionLoop();
        }
    }

    /// <summary>
    /// Attempts a single connection to the MQTT broker.
    /// Returns true if connected successfully.
    /// </summary>
    private async Task<bool> ConnectCoreAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_isConnected)
            {
                _logger.LogInformation("Already connected to MQTT broker");
                return true;
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

                    // Process any queued commands (fire-and-forget; tracked via _queueDrainTask for disposal)
                    _queueDrainTask = ProcessQueuedCommandsAsync(_disposalCts.Token);

                    return true;
                }

                _isConnected = false;
                connectionState.LastError = result.ResultCode.ToString();
                _logger.LogWarning("Failed to connect to MQTT broker: {ResultCode}",
                    result.ResultCode);
                _connectionStateSubject.OnNext(connectionState);

                throw new InvalidOperationException(
                    $"MQTT connection failed: {result.ResultCode}");
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw so Polly sees the failure
            }
            catch (Exception ex)
            {
                _isConnected = false;
                connectionState.LastError = ex.Message;
                _logger.LogError(ex, "Error connecting to MQTT broker");
                _connectionStateSubject.OnNext(connectionState);

                throw; // Re-throw so Polly sees the failure
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Launches a single reconnection loop if one is not already running.
    /// </summary>
    private void StartReconnectionLoop()
    {
        if (_reconnectionTask is null || _reconnectionTask.IsCompleted)
        {
            _reconnectionTask = ConnectWithRetryAsync(_disposalCts.Token);
        }
    }

    /// <summary>
    /// Reconnection loop driven by the Polly resilience pipeline.
    /// Runs as a single long-lived task until connection succeeds or cancellation is requested.
    /// </summary>
    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        try
        {
            await _reconnectionPipeline.ExecuteAsync(async token =>
            {
                _logger.LogInformation(
                    "Attempting reconnection (attempt {Attempt})", _connectionAttempt + 1);
                await ConnectCoreAsync(token);
            }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Reconnection loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconnection attempts exhausted");
        }
    }

    /// <summary>
    /// Handles detection of a dropped connection. Sets state, publishes disconnected status,
    /// and starts the reconnection loop.
    /// </summary>
    private void HandleConnectionLost(Exception ex)
    {
        if (!_connectionLock.Wait(0))
        {
            // A connect/disconnect is already in progress — it will handle state.
            _logger.LogDebug(ex, "Connection lost detected, but another operation holds the lock");
            return;
        }

        try
        {
            if (!_isConnected)
                return; // Already handled.

            _logger.LogWarning(ex, "Connection to MQTT broker lost");

            _isConnected = false;
            _mqttClient.ApplicationMessageReceivedAsync -= HandleMessageReceivedAsync;

            var connectionState = new MqttConnectionState
            {
                IsConnected = false,
                AttemptNumber = _connectionAttempt,
                LastError = ex.Message
            };
            _connectionStateSubject.OnNext(connectionState);
        }
        finally
        {
            _connectionLock.Release();
        }

        StartReconnectionLoop();
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
            EnqueueCommand(topic, payload);
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
        catch (MqttClientNotConnectedException ex)
        {
            HandleConnectionLost(ex);
            EnqueueCommand(topic, payload);
            throw;
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
        catch (MqttClientNotConnectedException ex)
        {
            HandleConnectionLost(ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Enqueues a command if the queue has capacity, otherwise discards it and logs an error.
    /// </summary>
    private void EnqueueCommand(string topic, string payload)
    {
        // Check capacity before enqueuing (slight over-admit is acceptable
        // under contention; the queue depth is a soft limit)
        if (Volatile.Read(ref _queuedCommandCount) >= _options.CommandQueueDepth)
        {
            _logger.LogError(
                "Command queue full ({QueueDepth}), discarding command: {Topic} = {Payload}",
                _options.CommandQueueDepth, topic, payload);
            return;
        }

        // Enqueue first so the item is dequeue-able before the drain loop
        // can observe the incremented count — prevents count underflow.
        _commandQueue.Enqueue((topic, payload));
        Interlocked.Increment(ref _queuedCommandCount);
        _logger.LogDebug("Queued command: {Topic} = {Payload}", topic, payload);
    }

    private async Task ProcessQueuedCommandsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && Volatile.Read(ref _isConnected) && _commandQueue.TryDequeue(out var command))
        {
            Interlocked.Decrement(ref _queuedCommandCount);
            try
            {
                _logger.LogInformation("Processing queued command: {Topic} = {Payload}",
                    command.Topic, command.Payload);
                await PublishAsync(command.Topic, command.Payload, ct);
            }
            catch (InvalidOperationException)
            {
                // PublishAsync already re-queued the command when not connected — stop draining
                break;
            }
            catch (MqttClientNotConnectedException)
            {
                // Connection lost during publish — command was re-queued by PublishAsync, stop draining
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued command");
                EnqueueCommand(command.Topic, command.Payload);
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

        // Signal reconnection loop to stop
        await _disposalCts.CancelAsync();

        // Wait for background tasks with a timeout
        var pendingTasks = new[] { _reconnectionTask, _queueDrainTask }
            .Where(t => t is not null && !t.IsCompleted)
            .Cast<Task>()
            .ToArray();

        if (pendingTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(pendingTasks).WaitAsync(DisposalTimeout);
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
