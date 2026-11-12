using System;
using System.ComponentModel;
using System.Windows;
using HandyControl.Tools;
using UxPlayDesktop.Services;
using UxPlayDesktop.ViewModels;

namespace UxPlayDesktop;

public partial class MainWindow : HandyControl.Controls.Window
{
    private ITrayIconService? _trayService;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();
        ApplyBackdrop();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Mica on Windows 11: the side pane sits right on the backdrop and the pages are a translucent layer over it.
    /// Without Mica (Windows 10) the window falls back to a solid background.
    /// </summary>
    private void ApplyBackdrop()
    {
        if (BackdropType.Mica.IsSupported())
        {
            SystemBackdropType = BackdropType.Mica;
            return;
        }

        SetResourceReference(BackgroundProperty, "Window.FallbackBrush");
    }

    public void InitializeTray()
    {
        if (_trayService == null && DataContext is MainViewModel vm)
        {
            _trayService = new TrayIconService();
            _trayService.Initialize(this, vm);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeTray();
        if (DataContext is MainViewModel vm)
        {
            vm.InitializeHotkeys(this);
        }
    }

    public void RequestCloseAndExit()
    {
        _isExplicitExit = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel vmExit && !_isExplicitExit && vmExit.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
            Hide();
            _trayService?.ShowNotification("UxPlay Desktop", "UxPlay is running in the background. Click this tray icon to restore.");
            return;
        }

        if (DataContext is MainViewModel vmCleanup)
        {
            vmCleanup.DisposeHotkeys();
        }

        _trayService?.Dispose();
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != WindowState.Minimized)
        {
            ShowInTaskbar = true;
        }
    }
}
