using CalledAssistant.Services;
using CalledAssistant.ViewModels;
using CalledAssistant.Views;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace CalledAssistant;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private AudioCaptureService? _audioCapture;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 应用不因最后一个窗口关闭而退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 初始化服务
        _settingsService = new SettingsService();
        _audioCapture = new AudioCaptureService();

        // 初始化系统托盘
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        // 初始化热键服务
        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkey();

        // 预创建 OverlayWindow（提升首次响应速度）
        EnsureOverlayWindow();
    }

    private void RegisterHotkey()
    {
        var ok = _hotkeyService!.Register(_settingsService!.Current.Hotkey);
        if (!ok)
        {
            MessageBox.Show(
                $"热键 [{_settingsService.Current.Hotkey.DisplayText}] 注册失败，可能被其他程序占用。\n请在设置中更改热键。",
                "Called Assistant",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void EnsureOverlayWindow()
    {
        if (_overlayWindow == null)
        {
            var vm = new OverlayViewModel(_settingsService!, _audioCapture!);
            _overlayWindow = new OverlayWindow(vm);
        }
    }

    private void OnHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (_overlayWindow?.IsVisible == true)
            {
                _overlayWindow.Hide();
                return;
            }

            EnsureOverlayWindow();
            _overlayWindow!.ShowAtCursor();
        });
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow?.IsVisible == true)
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = new SettingsViewModel(_settingsService!);
        vm.SettingsSaved += (s, _) =>
        {
            // 重新注册热键
            _hotkeyService!.Unregister();
            RegisterHotkey();

            // 重建 OverlayWindow（确保使用最新设置）
            if (_overlayWindow?.IsVisible == true)
                _overlayWindow.Hide();
            _overlayWindow = null;
            EnsureOverlayWindow();
        };

        _settingsWindow = new SettingsWindow(vm);
        _settingsWindow.Show();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _hotkeyService?.Dispose();
        _audioCapture?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _audioCapture?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

