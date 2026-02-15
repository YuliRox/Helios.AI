using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Interfaces;
using LumiRise.Api.Services.Mqtt.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Exceptions;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LumiRise.Api.Services.Mqtt.Implementation;

/// <summary>
/// Manages MQTT broker connection with timer-based health checking and reconnection.
/// Publishes connection state changes and message events as observables.
/// </summary>
public class MqttConnectionManager : IMqttConnectionManager
{
    private sealed record QueuedCommand(string Topic, string Payload, DateTimeOffset EnqueuedAtUtc);

    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly MqttOptions _options;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _clientOptions;
    private readonly Subject<MqttConnectionState> _connectionStateSubject = new();
    private readonly Subject<(string Topic, string Payload)> _messageReceivedSubject = new();
    private readonly ConcurrentDictionary<string, byte> _subscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<QueuedCommand> _commandQueue = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly object _monitorSync = new();
    private readonly object _queueDrainSync = new();

    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QueuedCommandMaxAge = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? _connectionMonitorCts;
    private Task? _connectionMonitorTask;
    private Task? _queueDrainTask;
    private int _connectionAttempt;
    private int _isConnected;
    private int _queuedCommandCount;
    private bool _disconnectRequested;
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
        _clientOptions = BuildClientOptions();

        _mqttClient.ApplicationMessageReceivedAsync += HandleMessageReceivedAsync;
    }

    public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

    public IObservable<MqttConnectionState> ConnectionState => _connectionStateSubject.AsObservable();

    public IObservable<(string Topic, string Payload)> MessageReceived => _messageReceivedSubject.AsObservable();

    public async Task ConnectAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        _disconnectRequested = false;
        StartConnectionMonitor();
        await TryConnectOnceAsync(ct);
    }

    /// <summary>
    /// Attempts a single connection to the MQTT broker and returns true on success.
    /// </summary>
    private async Task<bool> TryConnectOnceAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_disposed || _disconnectRequested)
            {
                return false;
            }

            if (_mqttClient.IsConnected)
            {
                if (Interlocked.Exchange(ref _isConnected, 1) == 0)
                {
                    _connectionStateSubject.OnNext(new MqttConnectionState
                    {
                        IsConnected = true,
                        AttemptNumber = Math.Max(_connectionAttempt, 1)
                    });
                }

                _connectionAttempt = 0;
                StartQueueDrain();
                return true;
            }

            _connectionAttempt++;
            var attempt = _connectionAttempt;

            try
            {
                var result = await _mqttClient.ConnectAsync(_clientOptions, ct);
                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    PublishDisconnectedState(attempt, result.ResultCode.ToString());
                    _logger.LogWarning(
                        "Failed to connect to MQTT broker at {Server}:{Port} (attempt {Attempt}): {ResultCode}",
                        _options.Server, _options.Port, attempt, result.ResultCode);
                    return false;
                }

                Interlocked.Exchange(ref _isConnected, 1);
                _connectionStateSubject.OnNext(new MqttConnectionState
                {
                    IsConnected = true,
                    AttemptNumber = attempt
                });

                _logger.LogInformation(
                    "Connected to MQTT broker at {Server}:{Port} on attempt {Attempt}",
                    _options.Server, _options.Port, attempt);

                _connectionAttempt = 0;
                await ResubscribeTopicsAsync(ct);
                StartQueueDrain();
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                PublishDisconnectedState(attempt, ex.Message);
                _logger.LogWarning(
                    ex,
                    "Error connecting to MQTT broker at {Server}:{Port} (attempt {Attempt})",
                    _options.Server, _options.Port, attempt);
                return false;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Keeps the connection alive and reconnects based on timer ticks.
    /// </summary>
    private async Task MonitorConnectionAsync(CancellationToken ct)
    {
        try
        {
            var tickIntervalMs = Math.Max(500, _options.ReconnectionDelayMs);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(tickIntervalMs));
            var consecutiveFailures = 0;

            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_disconnectRequested || _disposed)
                {
                    consecutiveFailures = 0;
                    continue;
                }

                var healthy = await EnsureConnectionHealthyAsync(ct);
                if (healthy)
                {
                    consecutiveFailures = 0;
                    continue;
                }

                consecutiveFailures++;

                if (_options.MaxReconnectionAttempts > 0 &&
                    consecutiveFailures >= _options.MaxReconnectionAttempts)
                {
                    _logger.LogError(
                        "MQTT reconnect stopped after reaching MaxReconnectionAttempts={Attempts}.",
                        _options.MaxReconnectionAttempts);
                    break;
                }

                var delay = CalculateReconnectDelay(consecutiveFailures - 1);
                _logger.LogInformation(
                    "MQTT reconnect attempt {NextAttempt} in {Delay}",
                    consecutiveFailures + 1, delay);
                await Task.Delay(delay, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("MQTT monitor loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in MQTT monitor loop");
        }
    }

    private async Task<bool> EnsureConnectionHealthyAsync(CancellationToken ct)
    {
        if (_mqttClient.IsConnected)
        {
            try
            {
                using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pingCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, _options.KeepAliveSeconds)));

                var pingOk = await _mqttClient.TryPingAsync(pingCts.Token);
                if (pingOk)
                {
                    if (Interlocked.Exchange(ref _isConnected, 1) == 0)
                    {
                        _connectionStateSubject.OnNext(new MqttConnectionState
                        {
                            IsConnected = true,
                            AttemptNumber = Math.Max(_connectionAttempt, 1)
                        });
                    }

                    StartQueueDrain();
                    return true;
                }

                MarkDisconnected("Ping failed");
                return false;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                MarkDisconnected("Ping timed out");
                return false;
            }
            catch (Exception ex)
            {
                MarkDisconnected(ex.Message, ex);
                return false;
            }
        }

        MarkDisconnected("Broker disconnected");
        return await TryConnectOnceAsync(ct);
    }

    private void StartConnectionMonitor()
    {
        lock (_monitorSync)
        {
            if (_disposed || _disposalCts.IsCancellationRequested)
            {
                return;
            }

            if (_connectionMonitorTask is not null && !_connectionMonitorTask.IsCompleted)
            {
                return;
            }

            _connectionMonitorCts?.Dispose();
            _connectionMonitorCts = CancellationTokenSource.CreateLinkedTokenSource(_disposalCts.Token);
            _connectionMonitorTask = MonitorConnectionAsync(_connectionMonitorCts.Token);
        }
    }

    private async Task StopConnectionMonitorAsync()
    {
        Task? taskToAwait = null;
        CancellationTokenSource? ctsToCancel = null;

        lock (_monitorSync)
        {
            if (_connectionMonitorCts is null)
            {
                return;
            }

            ctsToCancel = _connectionMonitorCts;
            taskToAwait = _connectionMonitorTask;
            _connectionMonitorCts = null;
            _connectionMonitorTask = null;
        }

        try
        {
            ctsToCancel.Cancel();

            if (taskToAwait is not null)
            {
                await taskToAwait;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("MQTT monitor loop stop acknowledged");
        }
        finally
        {
            ctsToCancel.Dispose();
        }
    }

    private void StartQueueDrain()
    {
        lock (_queueDrainSync)
        {
            if (_disposed || _disconnectRequested || _disposalCts.IsCancellationRequested || !IsConnected)
            {
                return;
            }

            if (_queueDrainTask is not null && !_queueDrainTask.IsCompleted)
            {
                return;
            }

            _queueDrainTask = ProcessQueuedCommandsAsync(_disposalCts.Token);
        }
    }

    private async Task ProcessQueuedCommandsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            if (!_commandQueue.TryDequeue(out var command))
            {
                return;
            }

            Interlocked.Decrement(ref _queuedCommandCount);

            if (DateTimeOffset.UtcNow - command.EnqueuedAtUtc > QueuedCommandMaxAge)
            {
                _logger.LogWarning(
                    "Discarding queued command older than {MaxAge}: {Topic} = {Payload}",
                    QueuedCommandMaxAge, command.Topic, command.Payload);
                continue;
            }

            try
            {
                await PublishCoreAsync(command.Topic, command.Payload, ct, enqueueOnDisconnect: false);
            }
            catch (InvalidOperationException)
            {
                RequeueCommand(command);
                return;
            }
            catch (MqttClientNotConnectedException)
            {
                RequeueCommand(command);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued command, will retry after reconnect");
                RequeueCommand(command);
                return;
            }
        }
    }

    private void EnqueueCommand(string topic, string payload)
    {
        if (_disconnectRequested || _disposed || _disposalCts.IsCancellationRequested)
        {
            return;
        }

        if (Volatile.Read(ref _queuedCommandCount) >= _options.CommandQueueDepth)
        {
            _logger.LogWarning(
                "Command queue full ({QueueDepth}), discarding command: {Topic} = {Payload}",
                _options.CommandQueueDepth, topic, payload);
            return;
        }

        _commandQueue.Enqueue(new QueuedCommand(topic, payload, DateTimeOffset.UtcNow));
        Interlocked.Increment(ref _queuedCommandCount);
        _logger.LogDebug("Queued command while disconnected: {Topic} = {Payload}", topic, payload);
    }

    private void RequeueCommand(QueuedCommand command)
    {
        if (_disconnectRequested || _disposed || _disposalCts.IsCancellationRequested)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - command.EnqueuedAtUtc > QueuedCommandMaxAge)
        {
            _logger.LogWarning(
                "Discarding queued command after reconnect timeout ({MaxAge}): {Topic} = {Payload}",
                QueuedCommandMaxAge, command.Topic, command.Payload);
            return;
        }

        if (Volatile.Read(ref _queuedCommandCount) >= _options.CommandQueueDepth)
        {
            _logger.LogWarning(
                "Command queue full ({QueueDepth}), discarding re-queued command: {Topic} = {Payload}",
                _options.CommandQueueDepth, command.Topic, command.Payload);
            return;
        }

        _commandQueue.Enqueue(command);
        Interlocked.Increment(ref _queuedCommandCount);
    }

    private void ClearQueuedCommands(string reason)
    {
        var removed = 0;
        while (_commandQueue.TryDequeue(out _))
        {
            removed++;
        }

        Interlocked.Exchange(ref _queuedCommandCount, 0);

        if (removed > 0)
        {
            _logger.LogInformation("Discarded {Count} queued MQTT command(s): {Reason}", removed, reason);
        }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Server, _options.Port)
            .WithClientId(_options.ClientId)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAliveSeconds));

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        return builder.Build();
    }

    private TimeSpan CalculateReconnectDelay(int failedAttemptIndex)
    {
        var minDelayMs = Math.Max(100, _options.ReconnectionDelayMs);
        var maxDelayMs = Math.Max(minDelayMs, _options.MaxReconnectionDelayMs);
        var multiplier = _options.BackoffMultiplier <= 0 ? 2.0 : _options.BackoffMultiplier;

        var exponential = minDelayMs * Math.Pow(multiplier, failedAttemptIndex);
        var capped = Math.Min(maxDelayMs, exponential);

        var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4);
        return TimeSpan.FromMilliseconds(capped * jitterFactor);
    }

    private void PublishDisconnectedState(int attempt, string? error)
    {
        Interlocked.Exchange(ref _isConnected, 0);
        _connectionStateSubject.OnNext(new MqttConnectionState
        {
            IsConnected = false,
            AttemptNumber = attempt,
            LastError = error
        });
    }

    private void MarkDisconnected(string? error, Exception? ex = null)
    {
        if (Interlocked.Exchange(ref _isConnected, 0) != 1)
        {
            return;
        }

        _connectionStateSubject.OnNext(new MqttConnectionState
        {
            IsConnected = false,
            AttemptNumber = _connectionAttempt,
            LastError = error
        });

        if (ex is null)
        {
            _logger.LogWarning("MQTT broker connection lost: {Error}", error);
        }
        else
        {
            _logger.LogWarning(ex, "MQTT broker connection lost: {Error}", error);
        }
    }

    private async Task ResubscribeTopicsAsync(CancellationToken ct)
    {
        if (_subscriptions.IsEmpty)
        {
            return;
        }

        var topics = _subscriptions.Keys.ToArray();
        foreach (var topic in topics)
        {
            await _mqttClient.SubscribeAsync(topic, cancellationToken: ct);
            _logger.LogDebug("Re-subscribed to {Topic}", topic);
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        _disconnectRequested = true;
        ClearQueuedCommands("explicit disconnect");
        await StopConnectionMonitorAsync();

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (!_mqttClient.IsConnected)
            {
                MarkDisconnected("Disconnected");
                return;
            }

            try
            {
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
                MarkDisconnected("Disconnected");
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

        await PublishCoreAsync(topic, payload, ct, enqueueOnDisconnect: true);
    }

    private async Task PublishCoreAsync(string topic, string payload, CancellationToken ct, bool enqueueOnDisconnect)
    {
        if (!IsConnected)
        {
            if (enqueueOnDisconnect)
            {
                EnqueueCommand(topic, payload);
            }

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
            if (enqueueOnDisconnect)
            {
                EnqueueCommand(topic, payload);
            }

            MarkDisconnected(ex.Message, ex);
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
        _subscriptions.TryAdd(topic, 0);

        if (!IsConnected)
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
            _logger.LogWarning(ex, "MQTT client disconnected while subscribing to {Topic}", topic);
            MarkDisconnected(ex.Message, ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to {Topic}", topic);
            throw;
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
        _disconnectRequested = true;
        ClearQueuedCommands("dispose");

        await _disposalCts.CancelAsync();
        await StopConnectionMonitorAsync();

        var pendingTasks = new[] { _connectionMonitorTask, _queueDrainTask }
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

        try
        {
            using var disconnectCts = new CancellationTokenSource(DisposalTimeout);
            await DisconnectAsync(disconnectCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting during disposal");
        }

        _mqttClient.ApplicationMessageReceivedAsync -= HandleMessageReceivedAsync;

        _disposalCts.Dispose();
        _mqttClient.Dispose();

        _connectionStateSubject.OnCompleted();
        _connectionStateSubject.Dispose();

        _messageReceivedSubject.OnCompleted();
        _messageReceivedSubject.Dispose();

        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
