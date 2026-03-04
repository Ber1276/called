using CalledAssistant.ViewModels;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CalledAssistant.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly OverlayViewModel _viewModel;

        public OverlayWindow(OverlayViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // 监听回答变化，以低优先级自动滚动到底部（不抢占 UI 渲染）
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OverlayViewModel.ResponseText))
                {
                    Dispatcher.InvokeAsync(
                        () => ResponseScroll.ScrollToEnd(),
                        DispatcherPriority.Background);
                }
            };
        }

        /// <summary>
        /// 显示窗口并居中于当前鼠标位置
        /// </summary>
        public void ShowAtCursor()
        {
            var screen = SystemParameters.WorkArea;
            var mousePos = GetMousePosition();

            double left = mousePos.X - Width / 2;
            double top = mousePos.Y - 80;

            // 防止超出屏幕边界
            left = Math.Max(screen.Left, Math.Min(left, screen.Right - Width));
            top = Math.Max(screen.Top, Math.Min(top, screen.Bottom - 400));

            Left = left;
            Top = top;

            Show();
            Activate();
            Focus();

            _viewModel.OnActivated();

            // 文字优先模式：等待布局完成后聚焦输入框
            if (_viewModel.IsTextMode)
            {
                Dispatcher.InvokeAsync(
                    () => { TextInputBox.Focus(); },
                    DispatcherPriority.Loaded);
            }
        }

        private static System.Windows.Point GetMousePosition()
        {
            GetCursorPos(out var p);
            return new System.Windows.Point(p.X, p.Y);
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                _viewModel.Cancel();
                e.Handled = true;
            }
            else if (e.Key == Key.Space && _viewModel.IsVoiceMode)
            {
                _viewModel.OnSpacePressed();
                e.Handled = true;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void TextInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return && Keyboard.Modifiers != ModifierKeys.Shift)
                e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.Cleanup();
        }
    }
}
