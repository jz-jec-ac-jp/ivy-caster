using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace IvyCaster.Console;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PcRow> _pcs = new()
    {
        new PcRow("pc-lab-01", "10.1.0.11", "Windows 11", "Online"),
        new PcRow("pc-lab-02", "10.1.0.12", "Windows 11", "Offline"),
        new PcRow("pc-lab-03", "10.1.0.13", "Windows 11", "Online")
    };

    public MainWindow()
    {
        InitializeComponent();
        PcDataGrid.ItemsSource = _pcs;
        WriteLog("コンソール起動");
    }

    private void DiscoverPcButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "PC一覧を更新しました";
        WriteLog("PC再検索を実行（ダミー）");
    }

    private void DeployAgentButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PcDataGrid.SelectedItem is not PcRow row)
        {
            StatusTextBlock.Text = "展開対象が未選択です";
            WriteLog("エージェント展開: 対象未選択");
            return;
        }

        StatusTextBlock.Text = $"{row.HostName} に展開要求を送信";
        WriteLog($"WMI初回展開を送信: host={row.HostName}, ip={row.IpAddress}");
    }

    private void HeartbeatButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PcDataGrid.SelectedItem is not PcRow row)
        {
            StatusTextBlock.Text = "疎通確認対象が未選択です";
            WriteLog("疎通確認: 対象未選択");
            return;
        }

        StatusTextBlock.Text = $"{row.HostName} に疎通確認";
        WriteLog($"ハートビート送信（ダミー）: host={row.HostName}");
    }

    private void PcDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PcDataGrid.SelectedItem is PcRow row)
        {
            SelectedPcTextBlock.Text = $"選択中: {row.HostName} ({row.IpAddress})";
            return;
        }

        SelectedPcTextBlock.Text = "選択中: なし";
    }

    private void WriteLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}

public sealed record PcRow(string HostName, string IpAddress, string OperatingSystem, string Status);