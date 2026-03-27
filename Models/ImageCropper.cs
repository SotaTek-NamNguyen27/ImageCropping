using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TestCutImage.Models
{
    public class ImageCropper
    {
        public static Mat LoadAndPreprocess(string imagePath, IProgress<string>? progress = null)
        {
            progress?.Report($"Đang tải ảnh gốc: {Path.GetFileName(imagePath)}...");
            Mat originalImage = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (originalImage.Empty())
            {
                progress?.Report($"[LỖI] Không thể đọc được ảnh: {imagePath}");
                return null;
            }

            progress?.Report($"Đã nạp ảnh thành công! Kích thước: {originalImage.Width} x {originalImage.Height}");
            return originalImage;
        }

        public static List<Rect> CalculateGridBoxes(Mat originalImage, IProgress<string>? progress = null)
        {
            List<Rect> boundingBoxes = new List<Rect>();

            int tileWidth = 2560;
            int tileHeight = 2560;
            int imgWidth = originalImage.Width;
            int imgHeight = originalImage.Height;

            int cols = (int)Math.Ceiling((double)imgWidth / tileWidth);
            int rows = (int)Math.Ceiling((double)imgHeight / tileHeight);

            progress?.Report($"Ảnh sẽ được chia thành {rows} hàng x {cols} cột.");

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int startX = x * tileWidth;
                    int startY = y * tileHeight;

                    int currentWidth = Math.Min(tileWidth, imgWidth - startX);
                    int currentHeight = Math.Min(tileHeight, imgHeight - startY);

                    if (currentWidth <= 0 || currentHeight <= 0) continue;

                    boundingBoxes.Add(new Rect(startX, startY, currentWidth, currentHeight));
                }
            }

            progress?.Report($"Đã tính toán xong tọa độ cho {boundingBoxes.Count} vùng cắt.");
            return boundingBoxes;
        }

        public static void CropAndSave(Mat originalImage, List<Rect> boundingBoxes, string outputDirectory, IProgress<string>? progress = null, string fileNamePrefix = "extracted_image_")
        {
            progress?.Report("\nBắt đầu cắt ảnh (Đang chạy đa luồng)...");

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            int savedCount = 0;
            int imgWidth = originalImage.Width;
            int imgHeight = originalImage.Height;

            bool filterByResolution = false;
            HashSet<int> targetIndices = new HashSet<int>();

            if (imgWidth == 15360 && imgHeight == 20480)
            {
                filterByResolution = true;
                targetIndices = new HashSet<int> { 2, 6 };
                progress?.Report("Áp dụng bộ lọc cho phân giải 15360x20480: Chỉ lấy ảnh cắt số 2 và 6.");
            }
            else if (imgWidth == 15360 && imgHeight == 10240)
            {
                filterByResolution = true;
                targetIndices = new HashSet<int> { 4, 8 };
                progress?.Report("Áp dụng bộ lọc cho phân giải 15360x10240: Chỉ lấy ảnh cắt số 4 và 8.");
            }

            var compressionParams = new ImageEncodingParam(ImwriteFlags.PngCompression, 3);

            Parallel.For(0, boundingBoxes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                int currentBoxIndex = i + 1;
                var rect = boundingBoxes[i];
                bool shouldSave = !filterByResolution || targetIndices.Contains(currentBoxIndex);

                if (shouldSave)
                {     
                    using (Mat croppedImage = new Mat(originalImage, rect))
                    {
                        string fileName = Path.Combine(outputDirectory, $"{fileNamePrefix}{currentBoxIndex:D2}.png");
                        Cv2.ImWrite(fileName, croppedImage, compressionParams);

                        
                        Interlocked.Increment(ref savedCount);
                        progress?.Report($"-> Đã lưu: {fileName} (Kích thước: {rect.Width} x {rect.Height})");
                    }
                }
            });

            progress?.Report($"\nHoàn tất! Đã trích xuất thành công {savedCount} tấm ảnh nhỏ (trong tổng số {boundingBoxes.Count} ô).");
        }
    }
}