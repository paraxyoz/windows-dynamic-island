using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.Services
{
    public sealed class ClipboardManager
    {
        private const string ClipboardFileName = "clipboard_history.json";
        private readonly SettingsManager _settings;
        private readonly List<ClipboardEntry> _entries;
        private bool _suppressNextUpdate;
        private string _lastCapturedText = string.Empty;

        public event Action HistoryChanged;

        public IReadOnlyList<ClipboardEntry> Entries => _entries;

        public ClipboardManager(SettingsManager settings)
        {
            _settings = settings;
            _entries = StorageService.LoadJson(ClipboardFileName, () => new List<ClipboardEntry>());
            TrimAndSort();
        }

        public void OnWindowsClipboardUpdated(IntPtr islandHwnd)
        {
            if (!_settings.Current.ClipboardMonitoringEnabled) return;
            if (_suppressNextUpdate)
            {
                _suppressNextUpdate = false;
                return;
            }

            try
            {
                // Ignore if clipboard contains password manager ignore markers
                var dataObj = Clipboard.GetDataObject();
                if (dataObj != null)
                {
                    if (dataObj.GetDataPresent("ClipboardViewerIgnore") ||
                        dataObj.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") ||
                        dataObj.GetDataPresent("CanIncludeInClipboardHistory"))
                    {
                        object canInclude = dataObj.GetData("CanIncludeInClipboardHistory");
                        if (canInclude is bool b && !b) return;
                        if (dataObj.GetDataPresent("ClipboardViewerIgnore")) return;
                    }
                }

                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                string sourceApp = NativeMethods.GetForegroundProcessName(islandHwnd);
                AddTextEntry(text, sourceApp);
            }
            catch
            {
                // Clipboard may be briefly locked by another process
            }
        }

        public bool AddTextEntry(string text, string sourceApp = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Length > 100_000)
            {
                text = text.Substring(0, 100_000);
            }

            if (_settings.Current.ClipboardFilterSensitiveData && LooksLikeSensitiveSecret(text, sourceApp))
            {
                return false;
            }

            if (text == _lastCapturedText && _entries.Count > 0 && _entries[0].Text == text)
            {
                _entries[0].CopiedAt = DateTime.Now;
                Save();
                return true;
            }

            _lastCapturedText = text;

            var existing = _entries.FirstOrDefault(e => e.Text == text);
            if (existing != null)
            {
                existing.CopiedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(sourceApp)) existing.SourceApp = sourceApp;
            }
            else
            {
                _entries.Insert(0, new ClipboardEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = text,
                    CopiedAt = DateTime.Now,
                    SourceApp = sourceApp ?? string.Empty
                });
            }

            TrimAndSort();
            Save();
            return true;
        }

        public static bool LooksLikeSensitiveSecret(string text, string sourceApp)
        {
            if (!string.IsNullOrEmpty(sourceApp))
            {
                string lowerApp = sourceApp.ToLowerInvariant();
                if (lowerApp.Contains("keepass") || lowerApp.Contains("bitwarden") ||
                    lowerApp.Contains("1password") || lowerApp.Contains("dashlane"))
                {
                    return true;
                }
            }

            string trimmed = text.Trim();
            // Common API keys / tokens
            if (trimmed.StartsWith("sk-") || trimmed.StartsWith("ghp_") ||
                trimmed.StartsWith("github_pat_") || trimmed.StartsWith("AIza") ||
                trimmed.StartsWith("eyJ"))
            {
                if (!trimmed.Contains(" ") && trimmed.Length >= 20) return true;
            }

            // Single-word high-complexity password pattern (8-64 chars, no spaces, mixed upper+lower+digit+symbol)
            if (trimmed.Length >= 10 && trimmed.Length <= 64 && !trimmed.Any(char.IsWhiteSpace) && !trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                bool hasUpper = trimmed.Any(char.IsUpper);
                bool hasLower = trimmed.Any(char.IsLower);
                bool hasDigit = trimmed.Any(char.IsDigit);
                bool hasSpecial = trimmed.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.' && c != '/');
                if (hasUpper && hasLower && hasDigit && hasSpecial)
                {
                    return true;
                }
            }

            // Credit card number pattern (16 digits)
            if (Regex.IsMatch(trimmed, @"^(?:\d[ -]*?){15,16}$"))
            {
                return true;
            }

            return false;
        }

        public void CopyEntryToSystemClipboard(ClipboardEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Text)) return;
            try
            {
                _suppressNextUpdate = true;
                _lastCapturedText = entry.Text;
                Clipboard.SetText(entry.Text);
                entry.CopiedAt = DateTime.Now;
                TrimAndSort();
                Save();
            }
            catch
            {
                _suppressNextUpdate = false;
            }
        }

        public void TogglePin(string id)
        {
            var item = _entries.FirstOrDefault(e => e.Id == id);
            if (item == null) return;
            item.IsPinned = !item.IsPinned;
            TrimAndSort();
            Save();
        }

        public void DeleteEntry(string id)
        {
            if (_entries.RemoveAll(e => e.Id == id) > 0)
            {
                Save();
            }
        }

        public void ClearAll(bool keepPinned = true)
        {
            if (keepPinned)
            {
                _entries.RemoveAll(e => !e.IsPinned);
            }
            else
            {
                _entries.Clear();
            }
            Save();
        }

        public List<ClipboardEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return _entries.ToList();
            }
            string q = query.Trim();
            return _entries
                .Where(e => (e.Text != null && e.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (e.SourceApp != null && e.SourceApp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private void TrimAndSort()
        {
            var pinned = _entries.Where(e => e.IsPinned).OrderByDescending(e => e.CopiedAt).ToList();
            var unpinned = _entries.Where(e => !e.IsPinned).OrderByDescending(e => e.CopiedAt).ToList();

            int max = Math.Max(10, _settings.Current.ClipboardMaxHistory);
            int allowedUnpinned = Math.Max(0, max - pinned.Count);
            if (unpinned.Count > allowedUnpinned)
            {
                unpinned = unpinned.Take(allowedUnpinned).ToList();
            }

            _entries.Clear();
            _entries.AddRange(pinned);
            _entries.AddRange(unpinned);
        }

        private void Save()
        {
            StorageService.SaveJson(ClipboardFileName, _entries);
            HistoryChanged?.Invoke();
        }
    }
}
