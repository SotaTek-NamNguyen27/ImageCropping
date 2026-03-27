using System.Windows;
using System.Windows.Controls;
using TestCutImage.Services;
using TestCutImage.ViewModels;

namespace TestCutImage
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var dialogService = new WindowsDialogService();
            DataContext = new MainViewModel(dialogService);
        }

        private void TxtLogs_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }
}