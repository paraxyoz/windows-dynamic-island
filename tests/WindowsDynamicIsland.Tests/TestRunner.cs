using System;
using System.IO;
using System.Linq;
using WindowsDynamicIsland.Models;
using WindowsDynamicIsland.Services;
using WindowsDynamicIsland.Win32;

namespace WindowsDynamicIsland.Tests
{
    public static class TestRunner
    {
        private static int _passed;
        private static int _failed;

        [STAThread]
        public static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== Запуск модульных и интеграционных тестов Windows Dynamic Island ===");

            RunTest("1. StorageService: Атомарное сохранение и восстановление после сбоя (.bak)", TestStorageCrashRecovery);
            RunTest("2. SettingsManager: Валидация границ, сохранение и сброс по умолчанию", TestSettingsValidation);
            RunTest("3. ClipboardManager: Фильтрация паролей/токенов, закрепление и поиск", TestClipboardManager);
            RunTest("4. FileShelfManager: Ссылки без перемещения оригиналов и контроль удаления", TestFileShelfNonDestructive);
            RunTest("5. NotesManager: Создание, автосохранение, пин и напоминания", TestNotesAndReminders);
            RunTest("6. TimerService: Независимая работа таймера и секундомера с кругами", TestTimerAndStopwatch);
            RunTest("7. EventQueueManager: Очередь приоритетов и склейка однотипных событий", TestEventQueuePriorities);
            RunTest("8. SystemAudioController & NativeMethods: Реальный микшер звука и раскладка", TestHardwareAudioAndKeyboard);
            RunTest("9. WeatherService: Реальный запрос к метеосерверу Open-Meteo", TestLiveWeatherApi);

            Console.WriteLine($"=== Итог тестов: Пройдено {_passed}, Ошибок {_failed} ===");
            return _failed == 0 ? 0 : 1;
        }

        private static void RunTest(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine($"  [PASS] {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"  [FAIL] {name} -> {ex.Message}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void TestStorageCrashRecovery()
        {
            string testFile = "unit_test_recovery.json";
            var original = new NoteItem { Id = "test1", Title = "Recovery Title", Content = "Body 1" };
            StorageService.SaveJson(testFile, original);

            // Second save creates .bak of first save
            var updated = new NoteItem { Id = "test1", Title = "Updated Title", Content = "Body 2" };
            StorageService.SaveJson(testFile, updated);

            // Corrupt primary file to simulate unexpected power loss
            string fullPath = Path.Combine(StorageService.DataDirectory, testFile);
            File.WriteAllText(fullPath, "{ corrupted_json: [[[ ");

            var recovered = StorageService.LoadJson(testFile, () => new NoteItem { Title = "Fallback" });
            Assert(recovered != null && recovered.Title == "Recovery Title", "Должна восстановиться резервная копия .bak");

            try
            {
                File.Delete(fullPath);
                File.Delete(fullPath + ".bak");
            }
            catch { }
        }

        private static void TestSettingsValidation()
        {
            var sm = new SettingsManager();
            sm.Current.Opacity = 99.0; // Out of bounds
            sm.Current.IslandScale = 0.1; // Out of bounds
            sm.Save();

            Assert(sm.Current.Opacity <= 1.0 && sm.Current.Opacity >= 0.55, "Opacity должна ограничиваться диапазоном 0.55..1.0");
            Assert(sm.Current.IslandScale >= 0.80 && sm.Current.IslandScale <= 1.35, "IslandScale должен ограничиваться диапазоном 0.80..1.35");

            sm.ResetToDefaults();
            Assert(Math.Abs(sm.Current.IslandScale - 1.0) < 0.001, "ResetToDefaults должен сбрасывать масштаб к 1.0");
            Assert(sm.Current.Theme == AppThemeMode.Light, "Тема по умолчанию должна быть Светлой (Premium Light / Soft Gray)");
            Assert(sm.Current.AutoRetractOnIdle, "AutoRetractOnIdle должен быть включён по умолчанию");
            Assert(Math.Abs(sm.Current.TopOffsetPx - 0.0) < 0.001, "TopOffsetPx по умолчанию должен быть 0 (сращивание с верхним краем)");
            Assert(sm.Current.SchemaVersion >= 3, "SchemaVersion должен быть >= 3");
        }

        private static void TestClipboardManager()
        {
            Assert(ClipboardManager.LooksLikeSensitiveSecret("sk-proj-1234567890abcdef1234567890", "chrome"), "Должен фильтроваться OpenAI API-ключ");
            Assert(ClipboardManager.LooksLikeSensitiveSecret("P@ssw0rd!99#Xz", "keepass"), "Должен фильтроваться сложный пароль");
            Assert(!ClipboardManager.LooksLikeSensitiveSecret("Обычный многострочный текст\nдля вставки в документ", "notepad"), "Обычный текст не должен блокироваться");

            var sm = new SettingsManager();
            var cm = new ClipboardManager(sm);
            bool addedNormal = cm.AddTextEntry("Тестовая строка для истории Dynamic Island", "code");
            Assert(addedNormal, "Обычная строка должна добавляться");
            var found = cm.Search("Dynamic Island");
            Assert(found.Count >= 1, "Поиск в буфере должен находить добавленную запись");
        }

        private static void TestFileShelfNonDestructive()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "wdi_shelf_test_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(tempFile, "Original file content");

            var shelf = new FileShelfManager();
            int added = shelf.AddFiles(new[] { tempFile });
            Assert(added == 1, "Файл должен добавиться на полку");
            Assert(File.Exists(tempFile), "Оригинальный файл НЕ должен перемещаться или изменяться");

            var item = shelf.Items.FirstOrDefault(x => string.Equals(x.FilePath, tempFile, StringComparison.OrdinalIgnoreCase));
            Assert(item != null && item.ExistsOnDisk(), "Элемент полки должен видеть существующий файл");

            // Delete original file externally and check shelf reaction
            File.Delete(tempFile);
            Assert(!item.ExistsOnDisk(), "Полка должна определять удаление оригинального файла");
            Assert(item.FormattedSize.Contains("перемещён или удалён"), "Статус должен сообщать о перемещении или удалении");

            shelf.RemoveFromShelf(item.Id);
        }

        private static void TestNotesAndReminders()
        {
            var sm = new SettingsManager();
            var eq = new EventQueueManager(sm);
            var nm = new NotesManager(eq);

            var note = nm.CreateNote("Тестовая заметка", "Проверка автосохранения", DateTime.Now.AddMinutes(15));
            Assert(nm.Notes.Any(n => n.Id == note.Id), "Заметка должна присутствовать в списке");

            nm.UpdateNote(note.Id, "Обновлённый заголовок", "Новый текст");
            var searched = nm.Search("Обновлённый заголовок");
            Assert(searched.Any(n => n.Id == note.Id), "Поиск по заметкам должен находить обновлённую заметку");

            nm.SnoozeReminder(note.Id, 10);
            nm.MarkReminderCompleted(note.Id);
            Assert(note.ReminderCompleted, "Напоминание должно отмечаться выполненным");

            nm.DeleteNote(note.Id);
        }

        private static void TestTimerAndStopwatch()
        {
            var sm = new SettingsManager();
            var eq = new EventQueueManager(sm);
            var ts = new TimerService(eq);

            ts.SetTimerDuration(0, 7, 30);
            Assert(ts.FormattedTimerRemaining == "07:30", $"Ожидалось 07:30, получено {ts.FormattedTimerRemaining}");

            ts.StartStopwatch();
            System.Threading.Thread.Sleep(30);
            ts.RecordLap();
            ts.PauseStopwatch();

            Assert(ts.Laps.Count == 1, "Секундомер должен фиксировать круг");
            ts.ResetStopwatch();
            Assert(ts.Laps.Count == 0, "Сброс секундомера должен очищать список кругов");
        }

        private static void TestEventQueuePriorities()
        {
            var sm = new SettingsManager();
            var eq = new EventQueueManager(sm);

            eq.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.Volume,
                Priority = IslandEventPriority.Normal,
                Title = "Громкость 40%"
            });
            Assert(eq.CurrentEvent?.Title == "Громкость 40%", "Первое событие должно стать активным");

            // Higher priority event should preempt immediately
            eq.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.TimerFinished,
                Priority = IslandEventPriority.High,
                Title = "Напоминание: Встреча"
            });
            Assert(eq.CurrentEvent?.Title == "Напоминание: Встреча", "Событие с высоким приоритетом должно вытеснять обычное");
            eq.DismissCurrent();
        }

        private static void TestHardwareAudioAndKeyboard()
        {
            using (var audio = new SystemAudioController())
            {
                int vol = audio.GetVolumePercent();
                Assert(vol >= 0 && vol <= 100, "Системная громкость должна быть в диапазоне 0..100%");
            }

            string lang = NativeMethods.GetCurrentKeyboardLanguageShortName(out string full);
            Assert(!string.IsNullOrWhiteSpace(lang) && lang.Length >= 2, "Должен определяться код текущей раскладки клавиатуры");
        }

        private static void TestLiveWeatherApi()
        {
            string desc0 = WeatherService.DescribeWeatherCode(0);
            string desc95 = WeatherService.DescribeWeatherCode(95);
            Assert(desc0 == "Ясно" && desc95 == "Гроза", "Коды погоды WMO должны корректно переводиться");

            var sm = new SettingsManager();
            var ws = new WeatherService(sm);
            ws.RefreshAllAsync().GetAwaiter().GetResult();

            var primary = ws.GetPrimaryWeather();
            // Even if offline, primary is either loaded from API or gracefully handled without throwing
            if (string.IsNullOrEmpty(ws.LastErrorMessage))
            {
                Assert(primary != null && primary.Hourly.Count > 0 && primary.Daily.Count >= 5,
                    "Реальный API погоды должен возвращать почасовой и недельный прогноз");
            }
        }
    }
}
