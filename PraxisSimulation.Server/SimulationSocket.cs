using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using PraxisSimulation.Core;

namespace PraxisSimulation.Server;

internal sealed class SimulationSocket(WebSocket socket, ILogger logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Guid sessionId = Guid.NewGuid();
    private readonly Channel<object> outgoing = Channel.CreateBounded<object>(new BoundedChannelOptions(2048)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    private CancellationTokenSource? runCancellation;
    private Task simulation = Task.CompletedTask;

    private void Publish(object message)
    {
        // TryWrite wartet nie auf Netzwerk-I/O. Bei Überlast wird der Lauf abgebrochen.
        if (!outgoing.Writer.TryWrite(message))
            throw new InvalidOperationException("Client liest zu langsam; Ereignispuffer ist voll.");
    }

    public async Task RunAsync(CancellationToken requestAborted)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        lifetime.CancelAfter(TimeSpan.FromMinutes(15));
        Publish(new { type = "ready", sessionId, protocolVersion = 1,
            timeUnit = "minutes", dayIndex = 0, maxDurationSeconds = 900 });
        var sending = SendAsync(lifetime.Token);
        var receiving = ReceiveAsync(lifetime.Token);
        try
        {
            await await Task.WhenAny(sending, receiving);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            logger.LogDebug(ex, "WebSocket-Sitzung {SessionId} beendet", sessionId);
        }
        finally
        {
            await lifetime.CancelAsync();
            if (runCancellation is not null) await runCancellation.CancelAsync();
            outgoing.Writer.TryComplete();
            foreach (var task in new[] { simulation, sending, receiving })
            {
                try { await task; }
                catch (Exception ex) { logger.LogDebug(ex, "Sitzung {SessionId} aufgeräumt", sessionId); }
            }
            runCancellation?.Dispose();
            if (socket.State == WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Sitzung beendet", timeout.Token); }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) { socket.Abort(); }
            }
            else socket.Abort();
        }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in outgoing.Reader.ReadAllAsync(cancellationToken))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        }
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested)
        {
            var count = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(count), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    Publish(new { type = "error", code = "text_required" });
                    return;
                }
                count += result.Count;
                if (count == buffer.Length && !result.EndOfMessage)
                {
                    Publish(new { type = "error", code = "message_too_large" });
                    return;
                }
            } while (!result.EndOfMessage);

            try
            {
                var command = JsonSerializer.Deserialize<Command>(buffer.AsSpan(0, count), JsonOptions);
                switch (command?.Type)
                {
                    case "start":
                        if (!simulation.IsCompleted)
                        {
                            Publish(new { type = "error", code = "already_running" });
                            break;
                        }
                        var speed = command.Speed ?? 60;
                        if (!double.IsFinite(speed) || speed is < 1 or > 600)
                        {
                            Publish(new { type = "error", code = "invalid_speed" });
                            break;
                        }
                        runCancellation?.Dispose();
                        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        var runId = Guid.NewGuid();
                        Publish(new { type = "started", sessionId, runId, speed, dayIndex = 0 });
                        var token = runCancellation.Token;
                        simulation = Task.Run(() => SimulateAsync(runId, speed, token), CancellationToken.None);
                        break;
                    case "stop":
                        if (runCancellation is not null) await runCancellation.CancelAsync();
                        await simulation;
                        break;
                    default:
                        Publish(new { type = "error", code = "unknown_command" });
                        break;
                }
            }
            catch (JsonException)
            {
                Publish(new { type = "error", code = "invalid_json" });
            }
        }
    }

    private async Task SimulateAsync(Guid runId, double speed, CancellationToken cancellationToken)
    {
        long sequence = 0;
        try
        {
            await LiveSimulation.RunAsync(ev => Publish(new
            {
                type = "event", sessionId, runId, sequence = ++sequence, dayIndex = 0, data = ev
            }), speed, cancellationToken);
            Publish(new { type = "completed", sessionId, runId, lastSequence = sequence });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!outgoing.Writer.TryWrite(new { type = "stopped", sessionId, runId, lastSequence = sequence }))
                socket.Abort();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulationslauf {RunId} fehlgeschlagen", runId);
            if (!outgoing.Writer.TryWrite(new { type = "error", code = "simulation_failed", sessionId, runId }))
                socket.Abort();
        }
    }

    private sealed record Command(string? Type, double? Speed);
}
