using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using WindowsDynamicIsland.Models;

namespace WindowsDynamicIsland.Services
{
    public sealed class NotesManager
    {
        private const string NotesFileName = "notes.json";
        private readonly List<NoteItem> _notes;
        private readonly DispatcherTimer _reminderCheckTimer;
        private readonly EventQueueManager _eventQueue;

        public event Action NotesChanged;

        public IReadOnlyList<NoteItem> Notes => _notes;

        public NotesManager(EventQueueManager eventQueue)
        {
            _eventQueue = eventQueue;
            _notes = StorageService.LoadJson(NotesFileName, () => new List<NoteItem>());
            SortNotes();

            _reminderCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _reminderCheckTimer.Tick += OnReminderCheckTick;
            _reminderCheckTimer.Start();
        }

        public NoteItem CreateNote(string title = "", string content = "", DateTime? reminderAt = null)
        {
            var note = new NoteItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title ?? string.Empty,
                Content = content ?? string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReminderAt = reminderAt,
                ReminderCompleted = false,
                ReminderNotified = false
            };
            _notes.Insert(0, note);
            SortNotes();
            Save();
            return note;
        }

        public void UpdateNote(string id, string title, string content, DateTime? reminderAt = null, bool updateReminder = false)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return;
            note.Title = title ?? string.Empty;
            note.Content = content ?? string.Empty;
            note.UpdatedAt = DateTime.Now;
            if (updateReminder)
            {
                note.ReminderAt = reminderAt;
                note.ReminderNotified = false;
                note.ReminderCompleted = false;
            }
            SortNotes();
            Save();
        }

        public void SetReminder(string id, DateTime? reminderAt)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return;
            note.ReminderAt = reminderAt;
            note.ReminderNotified = false;
            note.ReminderCompleted = false;
            note.UpdatedAt = DateTime.Now;
            Save();
        }

        public void MarkReminderCompleted(string id)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return;
            note.ReminderCompleted = true;
            note.ReminderNotified = true;
            note.ReminderAt = null;
            Save();
        }

        public void SnoozeReminder(string id, int minutes = 5)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return;
            note.ReminderAt = DateTime.Now.AddMinutes(Math.Max(1, minutes));
            note.ReminderNotified = false;
            note.ReminderCompleted = false;
            Save();
        }

        public void TogglePin(string id)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return;
            note.IsPinned = !note.IsPinned;
            SortNotes();
            Save();
        }

        public void DeleteNote(string id)
        {
            if (_notes.RemoveAll(n => n.Id == id) > 0)
            {
                Save();
            }
        }

        public List<NoteItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _notes.ToList();
            string q = query.Trim();
            return _notes
                .Where(n => (n.Title != null && n.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (n.Content != null && n.Content.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private void OnReminderCheckTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            bool changed = false;

            foreach (var note in _notes.ToList())
            {
                if (note.ReminderAt.HasValue && !note.ReminderCompleted && !note.ReminderNotified)
                {
                    if (now >= note.ReminderAt.Value)
                    {
                        note.ReminderNotified = true;
                        changed = true;
                        TriggerReminderEvent(note);
                    }
                }
            }

            if (changed)
            {
                Save();
            }
        }

        private void TriggerReminderEvent(NoteItem note)
        {
            string noteId = note.Id;
            string body = !string.IsNullOrWhiteSpace(note.Content) ? note.Content.Trim() : note.DisplayTitle;
            if (body.Length > 90) body = body.Substring(0, 90) + "…";

            _eventQueue?.Enqueue(new IslandEvent
            {
                EventType = IslandEventType.NoteReminder,
                Priority = IslandEventPriority.Critical,
                Title = "Напоминание: " + note.DisplayTitle,
                Subtitle = body,
                BadgeText = "ЗАМЕТКА",
                DurationMs = 9500,
                Mood = SmileyMood.NotificationAlert,
                PrimaryActionLabel = "Выполнено ✓",
                PrimaryAction = () =>
                {
                    MarkReminderCompleted(noteId);
                    _eventQueue.DismissCurrent();
                },
                SecondaryActionLabel = "Отложить +5м",
                SecondaryAction = () =>
                {
                    SnoozeReminder(noteId, 5);
                    _eventQueue.DismissCurrent();
                }
            });
        }

        private void SortNotes()
        {
            var sorted = _notes
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.UpdatedAt)
                .ToList();
            _notes.Clear();
            _notes.AddRange(sorted);
        }

        private void Save()
        {
            StorageService.SaveJson(NotesFileName, _notes);
            NotesChanged?.Invoke();
        }
    }
}
