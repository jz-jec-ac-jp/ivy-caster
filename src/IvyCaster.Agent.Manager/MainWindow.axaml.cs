using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Markup.Xaml;
using IvyCaster.Agent.Manager.Messages;
using IvyCaster.Agent.Manager.Services;
using IvyCaster.Core;
using System.Linq;

namespace IvyCaster.Agent.Manager;

public partial class MainWindow : Window
{
    private readonly AgentManagementClient _client = new();
    private readonly IPrivilegeElevationService _elevationService = new OperationElevationService();
    private TextBlock StatusSummary => this.FindControl<TextBlock>("StatusSummaryTextBlock")!;
    private TextBox StatusDetails => this.FindControl<TextBox>("StatusDetailsTextBox")!;
    private TextBox Logs => this.FindControl<TextBox>("LogsTextBox")!;

    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            await RefreshStatusAsync();
            await RefreshLogsAsync();
        };
    }

    private async void RefreshStatus_OnClick(object? sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async void RefreshLogs_OnClick(object? sender, RoutedEventArgs e)
    {
        await RefreshLogsAsync();
    }

    private async void StopAgent_OnClick(object? sender, RoutedEventArgs e)
    {
        await ExecutePrivilegedOperationAsync("stop");
    }

    private async void RestartAgent_OnClick(object? sender, RoutedEventArgs e)
    {
        await ExecutePrivilegedOperationAsync("restart");
    }

    private async Task RefreshStatusAsync()
    {
        var response = await _client.GetStatusAsync();
        if (!response.Success || response.Status is null)
        {
            StatusSummary.Text = $"状態: {TroubleshootingMessages.AgentNotReachable}";
            StatusDetails.Text = $"{TroubleshootingMessages.OperationFailedPrefix} {response.Message}";
            return;
        }

        var status = response.Status;
        StatusSummary.Text = $"状態: {status.State} | Host: {status.HostName}";
        StatusDetails.Text = string.Join(
            Environment.NewLine,
            [
                $"AgentId: {status.AgentId}",
                $"Host: {status.HostName}",
                $"State: {status.State}",
                $"CheckedAtUtc: {status.CheckedAtUtc:O}",
                $"ProcessId: {status.Process.ProcessId}",
                $"ProcessName: {status.Process.ProcessName}",
                $"StartedAtUtc: {status.Process.StartedAtUtc:O}",
                $"LastHeartbeatUtc: {status.Process.LastHeartbeatUtc:O}"
            ]);
    }

    private async Task RefreshLogsAsync()
    {
        var response = await _client.GetLogsAsync(100);
        if (!response.Success || response.Logs is null)
        {
            Logs.Text = $"{TroubleshootingMessages.OperationFailedPrefix} {response.Message}";
            return;
        }

        Logs.Text = string.Join(
            Environment.NewLine,
            response.Logs.Select(x => $"{x.TimestampUtc:HH:mm:ss} [{x.Level}] {x.Message}"));
    }

    private async Task ExecutePrivilegedOperationAsync(string operation)
    {
        var elevation = await _elevationService.ElevateAndExecuteAsync(operation);
        if (!elevation.Success)
        {
            var message = elevation.WasCancelled
                ? TroubleshootingMessages.ElevationRejected
                : TroubleshootingMessages.ElevationFailed;
            if (!string.IsNullOrWhiteSpace(elevation.Message))
            {
                message = $"{message}{Environment.NewLine}{Environment.NewLine}詳細: {elevation.Message}";
            }
            await ShowMessageAsync(message);
            return;
        }

        await ShowMessageAsync($"{TroubleshootingMessages.OperationSucceededPrefix} {operation}");
        await RefreshStatusAsync();
        await RefreshLogsAsync();
    }

    private async Task ShowMessageAsync(string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Width = 90
        };

        var dialog = new Window
        {
            Title = "IvyCaster.Agent.Manager",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}