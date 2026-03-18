using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using IvyCaster.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.AccessControl;
using System.Security.Principal;

namespace IvyCaster.Agent.Management;

public sealed class AgentManagementPipeServer(
    IAgentManagementService managementService,
    ILogger<AgentManagementPipeServer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Management pipe server started. pipe={PipeName}", AgentManagementDefaults.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var server = CreateServerStream();

                await server.WaitForConnectionAsync(stoppingToken);
                _ = Task.Run(() => HandleClientSessionAsync(server, stoppingToken), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex) when (IsBrokenPipe(ex))
            {
                logger.LogDebug("Management pipe disconnected by client.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Management pipe loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        logger.LogInformation("Management pipe server stopped.");
    }

    private async Task HandleClientSessionAsync(NamedPipeServerStream server, CancellationToken stoppingToken)
    {
        using (server)
        {
            try
            {
                await HandleConnectionAsync(server, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException ex) when (IsBrokenPipe(ex))
            {
                logger.LogDebug("Management pipe disconnected by client during session.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Management pipe session failed.");
            }
        }
    }

    private async Task HandleConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        var noBom = new UTF8Encoding(false);
        using var reader = new StreamReader(stream, noBom, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(stream, noBom, 1024, leaveOpen: true);

        var payload = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            var emptyResponse = new AgentManagementResponse(false, "Request payload is empty.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(emptyResponse, JsonOptions));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        logger.LogInformation("Management pipe request received: {Payload}", payload);

        AgentManagementRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AgentManagementRequest>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            var invalid = new AgentManagementResponse(false, "Request payload is invalid JSON.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(invalid, JsonOptions));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (request is null)
        {
            var nullReq = new AgentManagementResponse(false, "Request payload is missing.");
            await writer.WriteLineAsync(JsonSerializer.Serialize(nullReq, JsonOptions));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var response = await managementService.HandleAsync(request, cancellationToken);
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
        await writer.FlushAsync(cancellationToken);
        logger.LogInformation("Management pipe response sent for action: {Action}", request.Action);
    }

    private static bool IsBrokenPipe(IOException ex)
    {
        return ex.Message.Contains("Pipe is broken", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("pipe is being closed", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("closed pipe", StringComparison.OrdinalIgnoreCase);
    }

    private static NamedPipeServerStream CreateServerStream()
    {
        if (OperatingSystem.IsWindows())
        {
            var pipeSecurity = new PipeSecurity();
            var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                authenticatedUsers,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                AgentManagementDefaults.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);
        }

        return new NamedPipeServerStream(
            AgentManagementDefaults.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }
}
