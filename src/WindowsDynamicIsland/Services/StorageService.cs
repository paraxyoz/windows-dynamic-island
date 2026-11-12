using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WindowsDynamicIsland.Services
{
    public static class StorageService
    {
        internal static string IsolatedDataDirectory =>
            AppDomain.CurrentDomain.GetData("WindowsDynamicIsland.TestDataDirectory") as string;

        private static readonly object SyncLock = new object();
        private static readonly string AppDataFolder;
        private static readonly string LogFilePath;

        static StorageService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AppDataFolder = IsolatedDataDirectory ?? Path.Combine(localAppData, "WindowsDynamicIsland");
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }
            }
            catch
            {
            }
            LogFilePath = Path.Combine(AppDataFolder, "app.log");
        }

        public static string DataDirectory => AppDataFolder;

        public static void SaveJson<T>(string fileName, T data)
        {
            lock (SyncLock)
            {
                try
                {
                    if (!Directory.Exists(AppDataFolder))
                    {
                        Directory.CreateDirectory(AppDataFolder);
                    }

                    string filePath = Path.Combine(AppDataFolder, fileName);
                    string tempPath = filePath + ".tmp";
                    string backupPath = filePath + ".bak";

                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    string json = serializer.Serialize(data);

                    File.WriteAllText(tempPath, json, Encoding.UTF8);

                    if (File.Exists(filePath))
                        File.Replace(tempPath, filePath, backupPath);
                    else
                        File.Move(tempPath, filePath);
                }
                catch (Exception ex)
                {
                    LogError($"SaveJson({fileName})", ex);
                }
            }
        }

        public static T LoadJson<T>(string fileName, Func<T> defaultFactory)
        {
            lock (SyncLock)
            {
                string filePath = Path.Combine(AppDataFolder, fileName);
                string backupPath = filePath + ".bak";
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

                try
                {
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var result = serializer.Deserialize<T>(json);
                            if (result != null) return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"LoadJson primary({fileName})", ex);
                }

                // Crash recovery from .bak
                try
                {
                    if (File.Exists(backupPath))
                    {
                        string json = File.ReadAllText(backupPath, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var recovered = serializer.Deserialize<T>(json);
                            if (recovered != null)
                            {
                                LogInfo($"Recovered {fileName} from backup file.");
                                return recovered;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"LoadJson backup({fileName})", ex);
                }

                return defaultFactory != null ? defaultFactory() : default(T);
            }
        }

        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public static void LogError(string context, Exception ex)
        {
            WriteLog("ERROR", $"{context}: {ex?.GetType().Name} - {ex?.Message}");
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (SyncLock)
                {
                    if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 512 * 1024)
                    {
                        string oldLog = LogFilePath + ".old";
                        if (File.Exists(oldLog)) File.Delete(oldLog);
                        File.Move(LogFilePath, oldLog);
                    }
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
