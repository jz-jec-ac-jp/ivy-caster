using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using IvyCaster.Core;

namespace IvyCaster.Agent.Manager.Services;

public sealed class AgentManagementClient
{
    private const int ConnectTimeoutMs = 6000;
    private const int ReadTimeoutMs = 6000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<AgentManagementResponse> GetStatusAsync(CancellationToken cancellationToken = default)
        => SendAsync(new AgentManagementRequest("status"), cancellationToken);

    public Task<AgentManagementResponse> GetLogsAsync(int take = 100, CancellationToken cancellationToken = default)
        => SendAsync(new AgentManagementRequest("logs", take), cancellationToken);

    public Task<AgentManagementResponse> StopAsync(CancellationToken cancellationToken = default)
        => SendAsync(new AgentManagementRequest("stop"), cancellationToken);

    public Task<AgentManagementResponse> RestartAsync(CancellationToken cancellationToken = default)
        => SendAsync(new AgentManagementRequest("restart"), cancellationToken);

    private static async Task<AgentManagementResponse> SendAsync(
        AgentManagementRequest request,
        CancellationToken cancellationToken)
    {
        var isStateChangingOp = IsStopOrRestart(request);
        var maxAttempts = isStateChangingOp ? 1 : 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    AgentManagementDefaults.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await client.ConnectAsync(ConnectTimeoutMs, cancellationToken);

                var noBom = new UTF8Encoding(false);
                using var writer = new StreamWriter(client, noBom, 1024, leaveOpen: true);
                using var reader = new StreamReader(client, noBom, false, 1024, leaveOpen: true);

                var payload = JsonSerializer.Serialize(request, JsonOptions);
                await writer.WriteLineAsync(payload);
                await writer.FlushAsync(cancellationToken);

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(ReadTimeoutMs);
                var responsePayload = await reader.ReadLineAsync(readCts.Token);
                if (string.IsNullOrWhiteSpace(responsePayload))
                {
                    if (isStateChangingOp)
                    {
                        return new AgentManagementResponse(true, "Operation accepted; connection closed during service transition.");
                    }

                    throw new IOException("Empty response from agent.");
                }

                var response = JsonSerializer.Deserialize<AgentManagementResponse>(responsePayload, JsonOptions);
                return response ?? new AgentManagementResponse(false, "Invalid response from agent.");
            }
            catch (IOException ex) when (isStateChangingOp && IsPipeDisconnected(ex))
            {
                return new AgentManagementResponse(true, "Operation accepted; pipe closed during service transition.");
            }
            catch (Exception ex) when (isStateChangingOp && IsPipeDisconnected(ex))
            {
                return new AgentManagementResponse(true, "Operation accepted; pipe closed during service transition.");
            }
            catch (TimeoutException ex)
            {
                if (attempt == maxAttempts)
                {
                    return new AgentManagementResponse(false, $"Pipe timeout: {ex.Message}");
                }

                await Task.Delay(250, cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    return new AgentManagementResponse(false, $"Pipe read timeout: {ex.Message}");
                }

                await Task.Delay(250, cancellationToken);
            }
            catch (Exception ex)
            {
                if (attempt == maxAttempts)
                {
                    return new AgentManagementResponse(false, ex.Message);
                }

                await Task.Delay(250, cancellationToken);
            }
        }

        return new AgentManagementResponse(false, "Unexpected management client state.");
    }

    private static bool IsStopOrRestart(AgentManagementRequest request)
        => string.Equals(request.Action, "stop", StringComparison.OrdinalIgnoreCase)
        || string.Equals(request.Action, "restart", StringComparison.OrdinalIgnoreCase);

    private static bool IsPipeDisconnected(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current.Message.Contains("closed pipe", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("pipe is being closed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
