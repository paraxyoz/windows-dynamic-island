using System;
using System.IO;

namespace WindowsDynamicIsland.Models
{
    [Serializable]
    public class ShelfItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        public bool ExistsOnDisk()
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return false;
            return IsDirectory ? Directory.Exists(FilePath) : File.Exists(FilePath);
        }

        public void RefreshMetadata()
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            FileName = Path.GetFileName(FilePath);
            if (string.IsNullOrEmpty(FileName)) FileName = FilePath;

            if (Directory.Exists(FilePath))
            {
                IsDirectory = true;
                Extension = "ПАПКА";
                FileSizeBytes = 0;
            }
            else if (File.Exists(FilePath))
            {
                IsDirectory = false;
                string ext = Path.GetExtension(FilePath);
                Extension = string.IsNullOrEmpty(ext) ? "ФАЙЛ" : ext.TrimStart('.').ToUpperInvariant();
                try
                {
                    FileSizeBytes = new FileInfo(FilePath).Length;
                }
                catch
                {
                    FileSizeBytes = 0;
                }
            }
        }

        public string FormattedSize
        {
            get
            {
                if (!ExistsOnDisk()) return "Файл перемещён или удалён";
                if (IsDirectory) return "Папка";
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} Б";
                double kb = FileSizeBytes / 1024.0;
                if (kb < 1024) return $"{kb:0.#} КБ";
                double mb = kb / 1024.0;
                if (mb < 1024) return $"{mb:0.##} МБ";
                double gb = mb / 1024.0;
                return $"{gb:0.##} ГБ";
            }
        }
    }
}
