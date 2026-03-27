using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCutImage.Services
{
    public interface IDialogService
    {
        string[] ShowOpenFolderDialog(string title, bool multiselect);
        string ShowOpenFileDialog(string title, string defaultFileName);
        void ShowError(string message, string title = "Lỗi");
    }
}
