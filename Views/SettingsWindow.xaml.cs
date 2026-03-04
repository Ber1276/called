using CalledAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace CalledAssistant.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.SettingsSaved += (s, e) =>
            {
                System.Windows.MessageBox.Show("设置已保存！", "Called Assistant",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
        }

        private void HotkeyCaptureBorder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _viewModel.HandleKeyCapture(e);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
