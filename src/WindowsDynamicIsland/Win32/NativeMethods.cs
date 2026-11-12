using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsDynamicIsland.Win32
{
    public static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_HOTKEY = 0x0312;
        public const int WM_DEVICECHANGE = 0x0219;
        public const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
        public const int WM_INPUTLANGCHANGE = 0x0051;
        public const int WM_DISPLAYCHANGE = 0x007E;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;


        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int DBT_DEVNODES_CHANGED = 0x0007;

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;
        public const int VK_CAPITAL = 0x14;
        public const int VK_SPACE = 0x20;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const int VK_NUMLOCK = 0x90;
        public const int VK_LSHIFT = 0xA0;
        public const int VK_RSHIFT = 0xA1;
        public const int VK_LCONTROL = 0xA2;
        public const int VK_RCONTROL = 0xA3;
        public const int VK_LMENU = 0xA4;
        public const int VK_RMENU = 0xA5;
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;
        public const int VK_MEDIA_PLAY_PAUSE = 0xB3;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        public static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        [DllImport("user32.dll")]
        public static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public static bool IsCapsLockOn()
        {
            return (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
        }

        public static string GetForegroundProcessName(IntPtr ignoreHwnd = default(IntPtr))
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == ignoreHwnd) return "Система";
                GetWindowThreadProcessId(fg, out uint pid);
                if (pid == 0) return "Система";
                using (var proc = Process.GetProcessById((int)pid))
                {
                    return string.IsNullOrWhiteSpace(proc.ProcessName) ? "Система" : proc.ProcessName;
                }
            }
            catch
            {
                return "Система";
            }
        }

        public static bool IsNumLockOn()
        {
            return (GetKeyState(VK_NUMLOCK) & 0x0001) != 0;
        }

        public static void ToggleCapsLock()
        {
            keybd_event((byte)VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event((byte)VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static uint GetActiveInputThreadId()
        {
            IntPtr fg = GetForegroundWindow();
            uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;

            var gti = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
            if (fgThread != 0 && GetGUIThreadInfo(fgThread, ref gti))
            {
                if (gti.hwndFocus != IntPtr.Zero)
                {
                    uint focusThread = GetWindowThreadProcessId(gti.hwndFocus, out _);
                    if (focusThread != 0) return focusThread;
                }
                if (gti.hwndActive != IntPtr.Zero)
                {
                    uint activeThread = GetWindowThreadProcessId(gti.hwndActive, out _);
                    if (activeThread != 0) return activeThread;
                }
            }

            return fgThread;
        }

        public static string GetCurrentKeyboardLanguageShortName(out string fullName)
        {
            fullName = "English (US)";
            try
            {
                uint threadId = GetActiveInputThreadId();
                IntPtr hkl = GetKeyboardLayout(threadId);
                int langId = (ushort)((long)hkl & 0xFFFF);
                if (langId == 0) langId = 0x0409;

                var culture = new System.Globalization.CultureInfo(langId);
                fullName = culture.NativeName;
                if (!string.IsNullOrEmpty(fullName))
                {
                    fullName = char.ToUpper(fullName[0]) + fullName.Substring(1);
                }
                return culture.TwoLetterISOLanguageName.ToUpperInvariant();
            }
            catch
            {
                return "EN";
            }
        }

        public static void SwitchToNextKeyboardLayout(IntPtr islandHwnd)
        {
            try
            {
                int count = GetKeyboardLayoutList(0, null);
                if (count <= 1) return;
                IntPtr[] layouts = new IntPtr[count];
                GetKeyboardLayoutList(count, layouts);

                uint threadId = GetActiveInputThreadId();
                IntPtr current = GetKeyboardLayout(threadId);

                int nextIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    if (((long)layouts[i] & 0xFFFF) == ((long)current & 0xFFFF))
                    {
                        nextIndex = (i + 1) % count;
                        break;
                    }
                }

                IntPtr targetHkl = layouts[nextIndex];
                ActivateKeyboardLayout(targetHkl, 0);

                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    PostMessage(fg, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);
                }
                if (islandHwnd != IntPtr.Zero && islandHwnd != fg)
                {
                    PostMessage(islandHwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);
                }
            }
            catch
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInterfaceFilter
        {
            public int Size;
            public int DeviceType;
            public int Reserved;
            public Guid ClassGuid;
            public short Name;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, ref DeviceInterfaceFilter filter, int flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterDeviceNotification(IntPtr registration);

        public static IntPtr RegisterUsbDeviceNotifications(IntPtr window)
        {
            var filter = new DeviceInterfaceFilter
            {
                Size = Marshal.SizeOf(typeof(DeviceInterfaceFilter)), DeviceType = 5,
                ClassGuid = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED")
            };
            return RegisterDeviceNotification(window, ref filter, 0);
        }

        public static bool IsForegroundWindowFullscreen(IntPtr islandHwnd)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == islandHwnd) return false;

                var sb = new StringBuilder(64);
                GetClassName(fg, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "XamlExplorerHostIslandWindow")
                    return false;

                IntPtr hMonFg = MonitorFromWindow(fg, 2); // MONITOR_DEFAULTTONEAREST
                if (hMonFg == IntPtr.Zero) return false;

                // Only hide the island if the fullscreen app is on the SAME monitor as the island!
                if (islandHwnd != IntPtr.Zero)
                {
                    IntPtr hMonIsland = MonitorFromWindow(islandHwnd, 2);
                    if (hMonIsland != IntPtr.Zero && hMonFg != hMonIsland)
                    {
                        return false;
                    }
                }

                if (!GetWindowRect(fg, out RECT winRect)) return false;

                var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(hMonFg, ref mi)) return false;

                return winRect.Left <= mi.rcMonitor.Left &&
                       winRect.Top <= mi.rcMonitor.Top &&
                       winRect.Right >= mi.rcMonitor.Right &&
                       winRect.Bottom >= mi.rcMonitor.Bottom;
            }
            catch
            {
                return false;
            }
        }

        public static ImageSource ExtractFileIcon(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
                using (Icon ico = Icon.ExtractAssociatedIcon(filePath))
                {
                    if (ico == null) return null;
                    var img = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    img.Freeze();
                    return img;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
