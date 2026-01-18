using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace OyasumiHidInspector
{
    public class HotkeyManager : IDisposable
    {
        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc _hookProc;
        private HashSet<Keys> _currentCombination = new HashSet<Keys>();
        private bool _isCombinationPressed = false;
        private object _lock = new object();

        public event Action HotkeyTriggered;

        public HotkeyManager()
        {
            _hookProc = KeyboardHookProc;
            SetupHook();
        }

        public void UpdateCombination(string hotkeyString)
        {
            lock (_lock)
            {
                _currentCombination.Clear();

                if (string.IsNullOrEmpty(hotkeyString))
                    return;

                var parts = hotkeyString.Split('+');
                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    if (Enum.TryParse<Keys>(trimmedPart, true, out var key))
                    {
                        _currentCombination.Add(key);
                    }
                }
            }
        }

        private void SetupHook()
        {
            try
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                    _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    CheckHotkey(key);
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    ResetHotkeyState(key);
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private void CheckHotkey(Keys keyPressed)
        {
            lock (_lock)
            {
                if (_currentCombination.Count == 0)
                    return;

                bool allKeysPressed = true;

                foreach (var requiredKey in _currentCombination)
                {
                    if (requiredKey == Keys.ControlKey || requiredKey == Keys.LControlKey || requiredKey == Keys.RControlKey)
                    {
                        if ((GetAsyncKeyState(Keys.ControlKey) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.LControlKey) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.RControlKey) & 0x8000) == 0)
                        {
                            allKeysPressed = false;
                            break;
                        }
                    }
                    else if (requiredKey == Keys.ShiftKey || requiredKey == Keys.LShiftKey || requiredKey == Keys.RShiftKey)
                    {
                        if ((GetAsyncKeyState(Keys.ShiftKey) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.LShiftKey) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.RShiftKey) & 0x8000) == 0)
                        {
                            allKeysPressed = false;
                            break;
                        }
                    }
                    else if (requiredKey == Keys.Menu || requiredKey == Keys.LMenu || requiredKey == Keys.RMenu)
                    {
                        if ((GetAsyncKeyState(Keys.Menu) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.LMenu) & 0x8000) == 0 &&
                            (GetAsyncKeyState(Keys.RMenu) & 0x8000) == 0)
                        {
                            allKeysPressed = false;
                            break;
                        }
                    }
                    else
                    {
                        if ((GetAsyncKeyState(requiredKey) & 0x8000) == 0)
                        {
                            allKeysPressed = false;
                            break;
                        }
                    }
                }

                if (allKeysPressed && !_isCombinationPressed)
                {
                    _isCombinationPressed = true;
                    HotkeyTriggered?.Invoke();
                }
            }
        }

        private void ResetHotkeyState(Keys releasedKey)
        {
            lock (_lock)
            {
                if (_currentCombination.Contains(releasedKey) ||
                    IsModifierKey(releasedKey))
                {
                    _isCombinationPressed = false;
                }
            }
        }

        private bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu;
        }

        public void Dispose()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);
    }
}
