using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class FileShelfManager
    {
        private const string ShelfFileName = "file_shelf.json";
        private readonly List<ShelfItem> _items;

        public event Action ShelfChanged;

        public IReadOnlyList<ShelfItem> Items => _items;

        public FileShelfManager()
        {
            _items = StorageService.LoadJson(ShelfFileName, () => new List<ShelfItem>());
            foreach (var item in _items)
            {
                item.RefreshMetadata();
            }
        }

        public int AddFiles(IEnumerable<string> paths)
        {
            if (paths == null) return 0;
            int added = 0;

            foreach (string rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath)) continue;
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch
                {
                    continue;
                }

                if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) continue;

                // Avoid duplicates; update timestamp if already on shelf
                var existing = _items.FirstOrDefault(x => string.Equals(x.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.AddedAt = DateTime.Now;
                    existing.RefreshMetadata();
                    added++;
                    continue;
                }

                var shelfItem = new ShelfItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FilePath = fullPath,
                    AddedAt = DateTime.Now
                };
                shelfItem.RefreshMetadata();
                _items.Insert(0, shelfItem);
                added++;
            }

            if (added > 0)
            {
                Save();
            }
            return added;
        }

        public void RemoveFromShelf(string id)
        {
            if (_items.RemoveAll(x => x.Id == id) > 0)
            {
                Save();
            }
        }

        public void ClearMissingFiles()
        {
            if (_items.RemoveAll(x => !x.ExistsOnDisk()) > 0)
            {
                Save();
            }
        }

        public void ClearAll()
        {
            _items.Clear();
            Save();
        }

        public bool OpenItem(ShelfItem item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null) return false;
            if (!item.ExistsOnDisk())
            {
                errorMessage = "Оригинальный файл был перемещён или удалён.";
                ShelfChanged?.Invoke();
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Не удалось открыть файл: " + ex.Message;
                return false;
            }
        }

        public bool RevealInExplorer(ShelfItem item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null) return false;
            if (!item.ExistsOnDisk())
            {
                errorMessage = "Оригинальный файл был перемещён или удалён.";
                ShelfChanged?.Invoke();
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{item.FilePath}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Не удалось открыть проводник: " + ex.Message;
                return false;
            }
        }

        public bool DeleteFromDisk(ShelfItem item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null) return false;
            try
            {
                if (item.IsDirectory)
                {
                    if (Directory.Exists(item.FilePath))
                    {
                        Directory.Delete(item.FilePath, true);
                    }
                }
                else
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                }
                _items.RemoveAll(x => x.Id == item.Id);
                Save();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Ошибка удаления: " + ex.Message;
                return false;
            }
        }

        public List<ShelfItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _items.ToList();
            string q = query.Trim();
            return _items
                .Where(x => (x.FileName != null && x.FileName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (x.Extension != null && x.Extension.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private void Save()
        {
            StorageService.SaveJson(ShelfFileName, _items);
            ShelfChanged?.Invoke();
        }
    }
}
