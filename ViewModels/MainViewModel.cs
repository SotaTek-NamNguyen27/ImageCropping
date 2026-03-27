using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TestCutImage.Models;
using TestCutImage.MVVM;
using TestCutImage.Services;

namespace TestCutImage.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;

        private string _inputFoldersText;
        public string InputFoldersText
        {
            get => _inputFoldersText;
            set => SetProperty(ref _inputFoldersText, value);
        }

        private string _outputFolder;
        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        private bool _saveSingleFolder;
        public bool SaveSingleFolder
        {
            get => _saveSingleFolder;
            set => SetProperty(ref _saveSingleFolder, value);
        }

        private string _logs = string.Empty;
        public string Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand BrowseInputCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand ProcessCommand { get; }

        public MainViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            BrowseInputCommand = new RelayCommand(_ => ExecuteBrowseInput());
            BrowseOutputCommand = new RelayCommand(_ => ExecuteBrowseOutput());
            ProcessCommand = new RelayCommand(async _ => await ExecuteProcessAsync(), _ => !IsProcessing);
        }

        private void ExecuteBrowseInput()
        {
            var folders = _dialogService.ShowOpenFolderDialog("Chọn (các) thư mục chứa ảnh gốc", true);
            if (folders != null && folders.Length > 0)
            {
                string current = InputFoldersText?.Trim() ?? string.Empty;
                var existing = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                foreach (var f in folders)
                {
                    if (!existing.Contains(f))
                    {
                        existing.Add(f);
                    }
                }
                InputFoldersText = string.Join(";", existing);
            }
        }

        private void ExecuteBrowseOutput()
        {
            var folder = _dialogService.ShowOpenFileDialog("Chọn thư mục lưu ảnh", "Chọn thư mục này.");
            if (!string.IsNullOrEmpty(folder))
            {
                OutputFolder = folder;
            }
        }

        private async Task ExecuteProcessAsync()
        {
            var inputFolders = (InputFoldersText ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                       .Select(f => f.Trim())
                                                       .ToList();

            if (inputFolders.Count == 0 || inputFolders.Any(f => !Directory.Exists(f)))
            {
                _dialogService.ShowError("Vui lòng chọn các thư mục ảnh gốc hợp lệ!");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFolder))
            {
                _dialogService.ShowError("Vui lòng chọn hoặc nhập thư mục lưu!");
                return;
            }

            IsProcessing = true;
            Logs = string.Empty;

            var progress = new Progress<string>(message =>
            {
                Logs += message + Environment.NewLine;
            });

            await Task.Run(() => ProcessImages(inputFolders, progress));

            IsProcessing = false;
        }

        private void ProcessImages(List<string> inputFolders, IProgress<string> progressReporter)
        {
            try
            {
                int countSuccess = 0, totalFiles = 0;

                foreach (var inputFolder in inputFolders)
                {
                    var filesInFolder = Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                        .Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLower()))
                        .ToList();

                    totalFiles += filesInFolder.Count;

                    if (filesInFolder.Count > 0)
                        progressReporter.Report($"\n[Thư mục: {Path.GetFileName(inputFolder)}] - Tìm thấy {filesInFolder.Count} ảnh.");

                    foreach (var file in filesInFolder)
                    {
                        string relativePath = Path.GetRelativePath(inputFolder, Path.GetDirectoryName(file));
                        string imageName = Path.GetFileNameWithoutExtension(file);

                        string currentOutputFolder = SaveSingleFolder
                            ? OutputFolder
                            : Path.Combine(OutputFolder, Path.GetFileName(inputFolder), relativePath == "." ? "" : relativePath, imageName);

                        progressReporter.Report($"\n--- Đang xử lý: {Path.GetFileName(file)} ---");
                        progressReporter.Report($"Thư mục lưu: {currentOutputFolder}");

                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                        using (var originalImage = ImageCropper.LoadAndPreprocess(file, progressReporter))
                        {
                            if (originalImage == null) continue;

                            var boundingBoxes = ImageCropper.CalculateGridBoxes(originalImage, progressReporter);

                            if (boundingBoxes.Count > 0)
                            {
                                ImageCropper.CropAndSave(originalImage, boundingBoxes, currentOutputFolder, progressReporter, $"{imageName}_");
                                countSuccess++;
                            }
                            else
                            {
                                progressReporter.Report("Không tìm thấy vùng cắt nào hợp lệ.");
                            }
                        }

                        stopwatch.Stop();
                        progressReporter.Report($"Thời gian xử lý: {stopwatch.ElapsedMilliseconds} ms");
                    }
                }

                if (totalFiles == 0)
                    progressReporter.Report("Không tìm thấy ảnh nào hợp lệ.");
                else
                    progressReporter.Report($"\n=== Xử lý hoàn tất. Thành công {countSuccess}/{totalFiles} ảnh. ===");
            }
            catch (Exception ex)
            {
                progressReporter.Report($"Đã có lỗi xảy ra: {ex.Message}\n{ex.StackTrace}");
            }
        }
    } 
}
