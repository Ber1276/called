using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalledAssistant.Services
{
    public class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private HwndSource? _hwndSource;
        private bool _registered = false;

        public event Action? HotkeyPressed;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // MOD_* 常量
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;

        public void Initialize()
        {
            // 创建隐藏窗口用于接收 WM_HOTKEY 消息
            var param = new HwndSourceParameters("HotkeyWindow")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0x800000, // WS_OVERLAPPED
                ExtendedWindowStyle = 0x00000080, // WS_EX_TOOLWINDOW (不在任务栏显示)
                PositionX = -100,
                PositionY = -100
            };

            _hwndSource = new HwndSource(param);
            _hwndSource.AddHook(WndProc);
        }

        public bool Register(Models.HotkeyConfig config)
        {
            if (_hwndSource == null) return false;

            // 先取消注册已有热键
            Unregister();

            uint modifiers = MOD_NOREPEAT;
            if (config.Alt) modifiers |= MOD_ALT;
            if (config.Ctrl) modifiers |= MOD_CONTROL;
            if (config.Shift) modifiers |= MOD_SHIFT;

            _registered = RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, modifiers, config.VirtualKey);
            return _registered;
        }

        public void Unregister()
        {
            if (_registered && _hwndSource != null)
            {
                UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
                _registered = false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource?.Dispose();
        }
    }
}
