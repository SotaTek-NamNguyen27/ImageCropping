using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TestCutImage
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Chọn (các) thư mục chứa ảnh gốc",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                string current = txtInputFolder.Text.Trim();
                string newFolders = string.Join(";", dialog.FolderNames);

                if (string.IsNullOrEmpty(current))
                {
                    txtInputFolder.Text = newFolders;
                }
                else
                {
                    var existing = current.Split(';').ToList();
                    foreach (var f in dialog.FolderNames)
                    {
                        if (!existing.Contains(f))
                        {
                            current += ";" + f;
                        }
                    }
                    txtInputFolder.Text = current;
                }
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn thư mục lưu ảnh",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Chọn thư mục này."
            };

            if (dialog.ShowDialog() == true)
            {
                txtOutputFolder.Text = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private async void BtnProcess_Click(object sender, RoutedEventArgs e)
        {
            string inputFolderText = txtInputFolder.Text;
            string outputFolder = txtOutputFolder.Text;
            bool saveSingleFolder = chkSingleFolder.IsChecked == true;

            var inputFolders = inputFolderText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(f => f.Trim())
                                              .ToList();

            if (inputFolders.Count == 0 || inputFolders.Any(f => !Directory.Exists(f)))
            {
                MessageBox.Show("Vui lòng chọn các thư mục ảnh gốc hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập thư mục lưu!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            btnProcess.IsEnabled = false;
            txtLogs.Clear();

            var progress = new Progress<string>(message =>
            {
                txtLogs.AppendText(message + Environment.NewLine);
                txtLogs.ScrollToEnd();
            });

            await Task.Run(() =>
            {
                try
                {
                    int countSuccess = 0;
                    int totalFiles = 0;
                    var progressReporter = (IProgress<string>)progress;

                    foreach (var inputFolder in inputFolders)
                    {
                        var filesInFolder = new List<string>();
                        foreach (var file in Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories))
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                            {
                                filesInFolder.Add(file);
                            }
                        }

                        totalFiles += filesInFolder.Count;

                        if (filesInFolder.Count > 0)
                        {
                            progressReporter.Report($"\n[Thư mục: {Path.GetFileName(inputFolder)}] - Tìm thấy {filesInFolder.Count} ảnh.");
                        }

                        foreach (var file in filesInFolder)
                        {
                            string relativePath = Path.GetRelativePath(inputFolder, Path.GetDirectoryName(file));
                            string imageName = Path.GetFileNameWithoutExtension(file);

                            string currentOutputFolder;
                            string filePrefix = $"{imageName}_";

                            if (saveSingleFolder)
                            {
                                currentOutputFolder = outputFolder;
                            }
                            else
                            {
                                string folderName = Path.GetFileName(inputFolder);
                                currentOutputFolder = relativePath == "."
                                    ? Path.Combine(outputFolder, folderName, imageName)
                                    : Path.Combine(outputFolder, folderName, relativePath, imageName);
                            }

                            progressReporter.Report($"\n--- Đang xử lý: {Path.GetFileName(file)} ---");
                            progressReporter.Report($"Thư mục lưu: {currentOutputFolder}");

                            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                            using (var originalImage = ImageCropper.LoadAndPreprocess(file, progress))
                            {
                                if (originalImage == null) continue;

                                var boundingBoxes = ImageCropper.CalculateGridBoxes(originalImage, progress);

                                if (boundingBoxes.Count > 0)
                                {
                                    ImageCropper.CropAndSave(originalImage, boundingBoxes, currentOutputFolder, progress, filePrefix);
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
                    {
                        progressReporter.Report("Không tìm thấy ảnh nào trong các thư mục này (chấp nhận: .jpg, .jpeg, .png, .bmp).");
                        return;
                    }

                    progressReporter.Report($"\n=== Xử lý hoàn tất. Thành công {countSuccess}/{totalFiles} ảnh. ===");
                }
                catch (Exception ex)
                {
                    ((IProgress<string>)progress).Report($"Đã có lỗi xảy ra: {ex.Message}\n{ex.StackTrace}");
                }
            });

            btnProcess.IsEnabled = true;
        }
    }
}