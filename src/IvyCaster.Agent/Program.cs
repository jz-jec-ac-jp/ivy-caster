using IvyCaster.Agent;
using IvyCaster.Agent.Management;
using IvyCaster.Agent.Platform;
using IvyCaster.Agent.Platform.Windows;
using IvyCaster.Agent.Runtime;
using IvyCaster.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var runtimeState = new AgentRuntimeState(Environment.MachineName, Environment.MachineName);
builder.Services.AddSingleton(runtimeState);

var logStore = new AgentMemoryLogStore();
builder.Services.AddSingleton(logStore);
builder.Services.AddSingleton<IAgentRuntimeLogStore>(logStore);
builder.Services.AddSingleton<ILoggerProvider>(logStore);

builder.Services.AddSingleton<IHeartbeatReporter, ConsoleHeartbeatReporter>();
builder.Services.AddSingleton<IAgentManagementService, AgentManagementService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
    builder.Services.AddSingleton<IAgentServiceController, WindowsAgentServiceController>();
    builder.Services.AddSingleton<IAgentLogProvider, WindowsAgentLogProvider>();
    builder.Services.AddSingleton<IAgentProcessInspector, WindowsAgentProcessInspector>();
}
else
{
    builder.Services.AddSingleton<IProcessRunner, IvyCaster.Agent.Platform.Linux.LinuxProcessRunner>();
    builder.Services.AddSingleton<IAgentServiceController, IvyCaster.Agent.Platform.Linux.LinuxAgentServiceController>();
    builder.Services.AddSingleton<IAgentLogProvider, IvyCaster.Agent.Platform.Linux.LinuxAgentLogProvider>();
    builder.Services.AddSingleton<IAgentProcessInspector, IvyCaster.Agent.Platform.Linux.LinuxAgentProcessInspector>();
}

builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddHostedService<AgentManagementPipeServer>();

var host = builder.Build();
await host.RunAsync();
