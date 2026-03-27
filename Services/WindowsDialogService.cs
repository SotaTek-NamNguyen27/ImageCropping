using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TestCutImage.Services
{
    public class WindowsDialogService : IDialogService
    {
        public void ShowError(string message, string title = "Lỗi")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public string ShowOpenFileDialog(string title, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true
            };
            return dialog.ShowDialog() == true ? System.IO.Path.GetDirectoryName(dialog.FileName) : null;
        }

        public string[] ShowOpenFolderDialog(string title, bool multiselect)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = title,
                Multiselect = multiselect
            };
            return dialog.ShowDialog() == true ? dialog.FolderNames : Array.Empty<string>();
        }
    }
}
