using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace WindowsDynamicIsland.Services
{
    internal static class BrowserDownloadHost
    {
        private sealed class Download
        {
            public string Id { get; set; }
            public string State { get; set; }
            public string Name { get; set; }
        }

        public static int Run()
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 4096, RecursionLimit = 6 };
            using (var input = new BinaryReader(Console.OpenStandardInput()))
            using (var output = new BinaryWriter(Console.OpenStandardOutput()))
            {
                while (true)
                {
                    int size;
                    try { size = input.ReadInt32(); }
                    catch (EndOfStreamException) { return 0; }
                    if (size <= 0 || size > 4096) return 1;
                    var bytes = input.ReadBytes(size);
                    if (bytes.Length != size) return 1;
                    bool accepted = false;
                    try
                    {
                        var item = serializer.Deserialize<Download>(Encoding.UTF8.GetString(bytes));
                        if (item != null && Regex.IsMatch(item.Id ?? "", @"\Aweb\.[a-z0-9.]{1,40}\z") &&
                            Regex.IsMatch(item.State ?? "", @"\A(?:[0-9]{1,2}|100|pending|failed|cancelled)\z") &&
                            !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 120)
                        {
                            string name = Path.GetFileName(item.Name).Replace('\r', ' ').Replace('\n', ' ').Replace('|', ' ');
                            if (!string.IsNullOrWhiteSpace(name))
                                accepted = SystemIntegrationService.SendIpcNotification($"TASK:{item.Id}|{item.State}|Загрузка · {name}");
                        }
                    }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                    var response = Encoding.UTF8.GetBytes(accepted ? "{\"ok\":true}" : "{\"ok\":false}");
                    output.Write(response.Length);
                    output.Write(response);
                    output.Flush();
                }
            }
        }
    }
}
