using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bl0ckedService
{
    /// <summary>
    /// Implements a low-level keyboard hook to block keyboard shortcuts
    /// while the lockdown is active. Prevents Alt+F4, Ctrl+Alt+Del, Win key, etc.
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookCallback;
        private bool _enabled = false;
        private readonly object _hookLock = new object();

        // Virtual key codes for blocked keys
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F4 = 0x73;
        private const int VK_L = 0x4C;
        private const int VK_D = 0x44;
        private const int VK_DELETE = 0x2E;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Installs the keyboard hook
        /// </summary>
        public void Install()
        {
            lock (_hookLock)
            {
                if (_hookId != IntPtr.Zero)
                    return; // Already installed

                _hookCallback = HookCallback;

                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule? curModule = curProcess.MainModule)
                {
                    if (curModule != null)
                    {
                        _hookId = SetWindowsHookEx(
                            WH_KEYBOARD_LL,
                            _hookCallback,
                            GetModuleHandle(curModule.ModuleName),
                            0
                        );
                    }
                }

                if (_hookId == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to install keyboard hook. Error code: {error}");
                }
            }
        }

        /// <summary>
        /// Uninstalls the keyboard hook
        /// </summary>
        public void Uninstall()
        {
            lock (_hookLock)
            {
                if (_hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Enables keyboard blocking (hook must be installed first)
        /// </summary>
        public void Enable()
        {
            lock (_hookLock)
            {
                _enabled = true;
            }
        }

        /// <summary>
        /// Disables keyboard blocking but keeps hook installed
        /// </summary>
        public void Disable()
        {
            lock (_hookLock)
            {
                _enabled = false;
            }
        }

        /// <summary>
        /// Gets whether keyboard blocking is currently enabled
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                lock (_hookLock)
                {
                    return _enabled;
                }
            }
        }

        /// <summary>
        /// Low-level keyboard hook callback that blocks specific key combinations
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (!_enabled)
            {
                // Not blocking - pass through
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Check modifier keys
                bool altPressed = (GetAsyncKeyState(0xA4) & 0x8000) != 0 || (GetAsyncKeyState(0xA5) & 0x8000) != 0; // VK_LMENU, VK_RMENU
                bool ctrlPressed = (GetAsyncKeyState(0xA2) & 0x8000) != 0 || (GetAsyncKeyState(0xA3) & 0x8000) != 0; // VK_LCONTROL, VK_RCONTROL
                bool shiftPressed = (GetAsyncKeyState(0xA0) & 0x8000) != 0 || (GetAsyncKeyState(0xA1) & 0x8000) != 0; // VK_LSHIFT, VK_RSHIFT

                // Block Windows keys (left and right)
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    return (IntPtr)1; // Block
                }

                // Block Alt+F4 (close window)
                if (altPressed && vkCode == VK_F4)
                {
                    return (IntPtr)1; // Block
                }

                // Block Alt+Tab (switch windows)
                if (altPressed && vkCode == VK_TAB)
                {
                    return (IntPtr)1; // Block
                }

                // Block Ctrl+Shift+Esc (Task Manager)
                if (ctrlPressed && shiftPressed && vkCode == VK_ESCAPE)
                {
                    return (IntPtr)1; // Block
                }

                // Block Ctrl+Alt+Del (Security screen)
                // Note: This combination is handled at a lower level by Windows and may not be fully blockable
                if (ctrlPressed && altPressed && vkCode == VK_DELETE)
                {
                    return (IntPtr)1; // Attempt to block
                }

                // Block Win+L (Lock screen)
                if (vkCode == VK_L)
                {
                    bool winPressed = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                    if (winPressed)
                    {
                        return (IntPtr)1; // Block
                    }
                }

                // Block Win+D (Show desktop)
                if (vkCode == VK_D)
                {
                    bool winPressed = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                    if (winPressed)
                    {
                        return (IntPtr)1; // Block
                    }
                }
            }

            // Pass through if not blocked
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Uninstall();
            GC.SuppressFinalize(this);
        }

        ~KeyboardHook()
        {
            Uninstall();
        }
    }
}
