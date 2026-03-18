using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

namespace IvyCaster.Agent.Manager;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private TrayIcons? _trayIcons;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closing += MainWindowOnClosing;
            desktop.MainWindow = _mainWindow;

            var trayReady = TryBuildTrayIcon();
            if (trayReady)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
            }
            desktop.ShutdownRequested += (_, _) =>
            {
                _isExiting = true;
                DisposeTray();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private bool TryBuildTrayIcon()
    {
        try
        {
            var menu = new NativeMenu();
            menu.Add(new NativeMenuItem("開く")
            {
                Command = new DelegatingCommand(ShowMainWindow)
            });
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(new NativeMenuItem("終了")
            {
                Command = new DelegatingCommand(ExitApplication)
            });

            _trayIcon = new TrayIcon
            {
                IsVisible = true,
                ToolTipText = "IvyCaster.Agent.Manager",
                Menu = menu
            };

            _trayIcons = [_trayIcon];
            TrayIcon.SetIcons(this, _trayIcons);
            _trayIcon.Clicked += (_, _) => ShowMainWindow();
            return true;
        }
        catch
        {
            DisposeTray();
            return false;
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        DisposeTray();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void MainWindowOnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void DisposeTray()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
        _trayIcons = null;
        TrayIcon.SetIcons(this, null);
    }

    private sealed class DelegatingCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}