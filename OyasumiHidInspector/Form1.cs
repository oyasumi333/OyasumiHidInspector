using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HidLibrary;

namespace OyasumiHidInspector
{
    public partial class Form1 : Form
    {
        private bool isLogging = false;
        private Dictionary<string, DeviceInfo> deviceInfos = new Dictionary<string, DeviceInfo>();
        private HashSet<string> blockedDevices = new HashSet<string>();
        private Dictionary<string, DateTime> lastKeyTime = new Dictionary<string, DateTime>();
        private Dictionary<string, StringBuilder> deviceBuffers = new Dictionary<string, StringBuilder>();
        private Dictionary<string, string> deviceNameCache = new Dictionary<string, string>();

        private const string RegistryKeyPath = @"SOFTWARE\OyasumiHidInspector";
        private const string BlockedDevicesValue = "BlockedDevices";

        public bool isPen = false;
        private HashSet<string> systemCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cmd", "regedit", "powershell", "taskmgr", "msconfig", "control",
            "compmgmt.msc", "services.msc", "eventvwr", "mmc", "gpedit.msc",
            "explorer", "runas", "shutdown", "logoff", "net","wmic",
            "diskpart", "format", "del", "rmdir", "attrib", "bcdedit",
            "reg", "regsvr32", "rundll32", "certutil", "vssadmin", "bdeunlock"
        };

        private IntPtr keyboardHook = IntPtr.Zero;

        private bool _isHotkeyPressed = false;
        private Keys[] _currentHotkeyKeys = Array.Empty<Keys>();
        private IntPtr _globalHotkeyHook = IntPtr.Zero;
        private bool _hotkeysEnabled = true;
        private object _hotkeyLock = new object();

        private LowLevelKeyboardProc _keyboardHookProc;
        private LowLevelKeyboardProc _globalHotkeyHookProc;

        public Form1()
        {
            InitializeComponent();

            _keyboardHookProc = new LowLevelKeyboardProc(KeyboardHookProc);
            _globalHotkeyHookProc = new LowLevelKeyboardProc(GlobalKeyboardHookProc);

            SetupRawInput();
            LoadBlockedDevices();
            SetupContextMenu();
            GetDevicesList();
            InitializeTimer();

            LoadSettings();
        }

        public int lim;
        private Timer timer;
        private void InitializeTimer()
        {
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            GetDevicesList();
        }

        private List<HidDevice> gldevices = new List<HidDevice>();
        private void GetDevicesList()
        {
            var allDevices = HidDevices.Enumerate();
            var uniqueDevices = new Dictionary<string, HidDevice>();

            foreach (var device in allDevices)
            {
                byte[] bs;
                device.ReadProduct(out bs);

                string name = "";
                foreach (byte b in bs)
                {
                    if (b > 0)
                        name += ((char)b).ToString();
                }

                if (name == "")
                    name = "Unknown device";
                if (!uniqueDevices.ContainsKey(name))
                {
                    uniqueDevices[name] = device;
                }
            }

            if (uniqueDevices.Count == lstHid.Items.Count)
                return;

            lstHid.Items.Clear();
            gldevices.Clear();
            foreach (var pair in uniqueDevices)
            {
                gldevices.Add(pair.Value);
                lstHid.Items.Add(pair.Key);
            }
        }

        private void SetupContextMenu()
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            ToolStripMenuItem unblockAllItem = new ToolStripMenuItem("Unblock all devices");
            unblockAllItem.Click += UnblockAllDevices_Click;
            contextMenu.Items.Add(unblockAllItem);

            ToolStripMenuItem clearLogItem = new ToolStripMenuItem("Clear log");
            clearLogItem.Click += (s, e) => listBoxLog.Items.Clear();
            contextMenu.Items.Add(clearLogItem);

            ToolStripMenuItem exportLogItem = new ToolStripMenuItem("Export log...");
            exportLogItem.Click += ExportLog_Click;
            contextMenu.Items.Add(exportLogItem);

            listBoxLog.ContextMenuStrip = contextMenu;
        }

        private void ExportLog_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                sfd.FileName = $"hid_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    using (StreamWriter writer = new StreamWriter(sfd.FileName))
                    {
                        foreach (var item in listBoxLog.Items)
                        {
                            writer.WriteLine(item.ToString());
                        }
                    }
                    MessageBox.Show($"Log exported to {sfd.FileName}", "Export done",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SetupRawInput()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];

            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x06;
            rid[0].dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY;
            rid[0].hwndTarget = this.Handle;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
            {
                throw new ApplicationException("Failed to register raw input device(s).");
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;
            const int WM_INPUT_DEVICE_CHANGE = 0x00FE;

            switch (m.Msg)
            {
                case WM_INPUT:
                    if (isLogging)
                        ProcessRawInput(m.LParam);
                    break;

                case WM_INPUT_DEVICE_CHANGE:
                    UpdateDeviceList();
                    break;
            }

            base.WndProc(ref m);
        }

        private void ProcessRawInput(IntPtr rawInputHandle)
        {
            uint dwSize = 0;
            GetRawInputData(rawInputHandle, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            if (dwSize == 0) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                {
                    RAWINPUT raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));

                    if (raw.header.dwType == RIM_TYPEKEYBOARD)
                    {
                        IntPtr deviceHandle = raw.header.hDevice;
                        string devicePath = GetDevicePath(deviceHandle);
                        string deviceId = GetDeviceId(devicePath);
                        string deviceName = GetDeviceDisplayName(devicePath);

                        ushort key = raw.data.VKey;
                        ushort flags = raw.data.Flags;
                        bool isKeyDown = (flags & RI_KEY_BREAK) == 0;

                        if (isKeyDown)
                        {
                            CheckHotkey((Keys)key);

                            bool isDeviceBlocked = IsDeviceBlocked(deviceId);

                            if (isDeviceBlocked)
                            {
                                AddLogEntry($"[{DateTime.Now:HH:mm:ss.fff}] [Blocked] {deviceName} - Key: {(Keys)key}");
                                return;
                            }

                            double interval = 0;
                            if (lastKeyTime.ContainsKey(deviceId))
                            {
                                interval = (DateTime.Now - lastKeyTime[deviceId]).TotalMilliseconds;
                            }

                            string keyChar = GetCharFromKey((Keys)key);
                            if (!string.IsNullOrEmpty(keyChar))
                            {
                                if (!deviceBuffers.ContainsKey(deviceId))
                                    deviceBuffers[deviceId] = new StringBuilder();

                                deviceBuffers[deviceId].Append(keyChar);

                                if (deviceBuffers[deviceId].Length >= 3)
                                {
                                    CheckForSystemCommands(deviceId, devicePath, interval);
                                }

                                if (deviceBuffers[deviceId].Length > 50)
                                {
                                    deviceBuffers[deviceId].Remove(0, deviceBuffers[deviceId].Length - 50);
                                }
                            }

                            string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{deviceName}] " +
                                            $"Key: {(Keys)key} - " +
                                            $"Interval: {interval:F1}ms";

                            lim = isPen ? 50 : 20;

                            if (interval > 0 && interval < lim)
                            {
                                logEntry += " [FAST]";

                                if (!deviceInfos.ContainsKey(deviceId))
                                    deviceInfos[deviceId] = new DeviceInfo();
                                deviceInfos[deviceId].LastKeyTime = DateTime.Now;
                                deviceInfos[deviceId].DeviceId = deviceId;
                                deviceInfos[deviceId].DevicePath = devicePath;
                            }

                            AddLogEntry(logEntry);
                            lastKeyTime[deviceId] = DateTime.Now;
                        }
                        else
                        {
                            ResetHotkeyState();
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ForceBlock()
        {
            AddLogEntry($"[{DateTime.Now:HH:mm:ss}] ForceBlock triggered! Hotkey: {SavedHotkey}");

            foreach (var deviceId in deviceInfos.Keys.ToList())
            {
                if (!IsDeviceBlocked(deviceId))
                {
                    var deviceInfo = deviceInfos[deviceId];
                    BlockDevice(deviceId, deviceInfo.DevicePath, "Forced block by hotkey");
                }
            }
        }

        private void ParseHotkeyCombination()
        {
            lock (_hotkeyLock)
            {
                if (string.IsNullOrEmpty(SavedHotkey))
                {
                    _currentHotkeyKeys = Array.Empty<Keys>();
                    return;
                }

                var keys = new List<Keys>();
                var parts = SavedHotkey.Split('+');

                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    if (Enum.TryParse<Keys>(trimmedPart, true, out var key))
                    {
                        keys.Add(key);
                    }
                }

                _currentHotkeyKeys = keys.ToArray();
            }
        }

        private void CheckHotkey(Keys keyPressed)
        {
            if (!_hotkeysEnabled || _currentHotkeyKeys.Length == 0) return;

            lock (_hotkeyLock)
            {
                bool controlPressed = (Control.ModifierKeys & Keys.Control) != 0;
                bool altPressed = (Control.ModifierKeys & Keys.Alt) != 0;
                bool shiftPressed = (Control.ModifierKeys & Keys.Shift) != 0;
                bool hasCtrl = SavedHotkey.ToUpper().Contains("CTRL");
                bool hasAlt = SavedHotkey.ToUpper().Contains("ALT");
                bool hasShift = SavedHotkey.ToUpper().Contains("SHIFT");

                bool isHotkey = true;

                if (hasCtrl && !controlPressed) isHotkey = false;
                if (hasAlt && !altPressed) isHotkey = false;
                if (hasShift && !shiftPressed) isHotkey = false;

                bool mainKeyMatch = false;
                foreach (var key in _currentHotkeyKeys)
                {
                    if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                        key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                        key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                        key == Keys.Control || key == Keys.Alt || key == Keys.Shift)
                    {
                        continue;
                    }

                    if (key == keyPressed)
                    {
                        mainKeyMatch = true;
                        break;
                    }
                }

                if (!mainKeyMatch) isHotkey = false;

                if (isHotkey && !_isHotkeyPressed)
                {
                    _isHotkeyPressed = true;
                    ForceBlock();
                }
            }
        }

        private void ResetHotkeyState()
        {
            _isHotkeyPressed = false;
        }
        private void SetupGlobalHotkeyHook()
        {
            if (_globalHotkeyHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_globalHotkeyHook);
                _globalHotkeyHook = IntPtr.Zero;
            }

            if (_currentHotkeyKeys.Length == 0) return;

            try
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                    _globalHotkeyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _globalHotkeyHookProc, moduleHandle, 0);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private IntPtr GlobalKeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _hotkeysEnabled)
            {
                lock (_hotkeyLock)
                {
                    if (_currentHotkeyKeys.Length == 0)
                        return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);

                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;

                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        bool controlPressed = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;
                        bool altPressed = (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;
                        bool shiftPressed = (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0;

                        bool hasCtrl = SavedHotkey.ToUpper().Contains("CTRL");
                        bool hasAlt = SavedHotkey.ToUpper().Contains("ALT");
                        bool hasShift = SavedHotkey.ToUpper().Contains("SHIFT");

                        bool isHotkey = true;

                        if (hasCtrl && !controlPressed) isHotkey = false;
                        if (hasAlt && !altPressed) isHotkey = false;
                        if (hasShift && !shiftPressed) isHotkey = false;

                        bool mainKeyMatch = false;
                        foreach (var hotkeyKey in _currentHotkeyKeys)
                        {
                            if (hotkeyKey == Keys.ControlKey || hotkeyKey == Keys.LControlKey || hotkeyKey == Keys.RControlKey ||
                                hotkeyKey == Keys.Menu || hotkeyKey == Keys.LMenu || hotkeyKey == Keys.RMenu ||
                                hotkeyKey == Keys.ShiftKey || hotkeyKey == Keys.LShiftKey || hotkeyKey == Keys.RShiftKey ||
                                hotkeyKey == Keys.Control || hotkeyKey == Keys.Alt || hotkeyKey == Keys.Shift)
                            {
                                continue;
                            }

                            if (hotkeyKey == key)
                            {
                                mainKeyMatch = true;
                                break;
                            }
                        }

                        if (!mainKeyMatch) isHotkey = false;

                        if (isHotkey && !_isHotkeyPressed)
                        {
                            _isHotkeyPressed = true;

                            this.BeginInvoke(new Action(() =>
                            {
                                ForceBlock();
                            }));
                        }
                    }
                    else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        foreach (var hotkeyKey in _currentHotkeyKeys)
                        {
                            if (hotkeyKey == key)
                            {
                                ResetHotkeyState();
                                break;
                            }
                        }
                    }
                }
            }

            return CallNextHookEx(_globalHotkeyHook, nCode, wParam, lParam);
        }

        private void CheckForSystemCommands(string deviceId, string devicePath, double interval)
        {
            if (!deviceBuffers.ContainsKey(deviceId)) return;

            string buffer = deviceBuffers[deviceId].ToString().ToLower();

            foreach (var cmd in systemCommands)
            {
                if (buffer.Contains(cmd.ToLower()))
                {
                    AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Warning: detected '{cmd}' on {deviceId}");

                    if (interval < lim)
                    {
                        BlockDevice(deviceId, devicePath, $"System command '{cmd}' with fast typing ({interval:F1}ms)");
                    }

                    deviceBuffers[deviceId].Clear();
                    break;
                }
            }
        }

        private void BlockDevice(string deviceId, string devicePath, string reason)
        {
            if (blockedDevices.Contains(deviceId)) return;

            blockedDevices.Add(deviceId);
            SaveBlockedDevices();

            if (!InstallHooks())
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Error setting hooks to block");
                return;
            }

            string deviceName = GetDeviceDisplayName(devicePath);
            string vidPid = ExtractVidPidFromDevicePath(devicePath);
            AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Device blocked: {deviceName}");

            if (!string.IsNullOrEmpty(vidPid))
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Device ID: {vidPid}");
            }
            else
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Device ID: {deviceId}");
            }

            AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Reason: {reason}");

            if (!deviceInfos.ContainsKey(deviceId))
                deviceInfos[deviceId] = new DeviceInfo();

            deviceInfos[deviceId].DeviceId = deviceId;
            deviceInfos[deviceId].DevicePath = devicePath;
            deviceInfos[deviceId].IsBlocked = true;

            ShowBlockNotification(deviceId, reason, deviceName, vidPid);
        }
        private string ExtractVidPidFromDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return string.Empty;

            try
            {
                var vidMatch = Regex.Match(devicePath, @"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                var pidMatch = Regex.Match(devicePath, @"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);

                if (vidMatch.Success && pidMatch.Success)
                {
                    return $"VID_{vidMatch.Groups[1].Value} PID_{pidMatch.Groups[1].Value}";
                }
            }
            catch
            {

            }

            return string.Empty;
        }

        private void ShowBlockNotification(string deviceId, string reason, string deviceName = null, string vidPid = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowBlockNotification(deviceId, reason, deviceName, vidPid)));
                return;
            }

            string message;

            if (!string.IsNullOrEmpty(deviceName) && !string.IsNullOrEmpty(vidPid))
            {
                message = $"Device blocked!\n\n" +
                         $"Name: {deviceName}\n" +
                         $"ID: {vidPid}\n" +
                         $"Reason: {reason}";
            }
            else if (!string.IsNullOrEmpty(deviceName))
            {
                message = $"Device blocked!\n\n" +
                         $"Name: {deviceName}\n" +
                         $"ID: {deviceId}\n" +
                         $"Reason: {reason}";
            }
            else
            {
                message = $"Device blocked!\n\n" +
                         $"ID: {deviceId}\n" +
                         $"Reason: {reason}";
            }

            MessageBox.Show(message,
                          "Device blocked",
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Warning);
        }

        private bool InstallHooks()
        {
            if (keyboardHook != IntPtr.Zero)
                return true;

            try
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                    keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookProc, moduleHandle, 0);

                    return keyboardHook != IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Error setting hooks: {ex.Message}");
                return false;
            }
        }

        private void UninstallHooks()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }

            if (_globalHotkeyHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_globalHotkeyHook);
                _globalHotkeyHook = IntPtr.Zero;
            }
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && blockedDevices.Any())
            {
                return (IntPtr)1;
            }

            return CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        private void UnblockAllDevices_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Unblock all devices?",
                "Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                blockedDevices.Clear();
                SaveBlockedDevices();

                foreach (var deviceInfo in deviceInfos.Values)
                {
                    deviceInfo.IsBlocked = false;
                }
                deviceBuffers.Clear();

                UninstallHooks();

                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] All devices unblocked");
            }
        }

        private string GetDeviceDisplayName(string devicePath)
        {
            if (deviceNameCache.ContainsKey(devicePath))
            {
                return deviceNameCache[devicePath];
            }

            string displayName = "Unknown device";

            try
            {
                var allDevices = HidDevices.Enumerate();
                var device = allDevices.FirstOrDefault(d =>
                    d.DevicePath != null && d.DevicePath.Equals(devicePath, StringComparison.OrdinalIgnoreCase));

                if (device != null)
                {
                    byte[] bs;
                    if (device.ReadProduct(out bs))
                    {
                        string name = "";
                        foreach (byte b in bs)
                        {
                            if (b > 0)
                                name += ((char)b).ToString();
                        }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            displayName = name.Trim();
                        }
                    }
                }
                else
                {
                    var vidMatch = Regex.Match(devicePath, @"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                    var pidMatch = Regex.Match(devicePath, @"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);

                    if (vidMatch.Success && pidMatch.Success)
                    {
                        ushort vid = Convert.ToUInt16(vidMatch.Groups[1].Value, 16);
                        ushort pid = Convert.ToUInt16(pidMatch.Groups[1].Value, 16);

                        var matchedDevice = allDevices.FirstOrDefault(d =>
                            d.Attributes.VendorId == vid && d.Attributes.ProductId == pid);

                        if (matchedDevice != null)
                        {
                            byte[] bs;
                            if (matchedDevice.ReadProduct(out bs))
                            {
                                string name = "";
                                foreach (byte b in bs)
                                {
                                    if (b > 0)
                                        name += ((char)b).ToString();
                                }

                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    displayName = name.Trim();
                                }
                            }
                        }
                    }
                    else
                    {
                        displayName = devicePath.ToLower().Contains("keyboard") ? "Keyboard" : "HID Device";
                    }
                }
            }
            catch (Exception ex)
            {
                displayName = devicePath.ToLower().Contains("keyboard") ? "Keyboard" : "HID Device";
            }

            deviceNameCache[devicePath] = displayName;
            return displayName;
        }

        private string GetDevicePath(IntPtr hDevice)
        {
            uint pcbSize = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);

            if (pcbSize == 0) return string.Empty;

            IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
            try
            {
                uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pData, ref pcbSize);
                if (result > 0 && result != uint.MaxValue)
                {
                    return Marshal.PtrToStringAnsi(pData);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }

            return string.Empty;
        }

        private string GetDeviceId(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return "unknown";

            var vidMatch = Regex.Match(devicePath, @"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(devicePath, @"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);

            if (vidMatch.Success && pidMatch.Success)
            {
                return $"VID_{vidMatch.Groups[1].Value}_PID_{pidMatch.Groups[1].Value}";
            }

            return devicePath.GetHashCode().ToString("X8");
        }

        private bool IsDeviceBlocked(string deviceId)
        {
            return blockedDevices.Contains(deviceId);
        }

        private void LoadBlockedDevices()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        string[] devices = key.GetValue(BlockedDevicesValue) as string[];
                        if (devices != null)
                        {
                            blockedDevices = new HashSet<string>(devices);

                            if (blockedDevices.Count > 0)
                            {
                                InstallHooks();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Error loading blocked devices: {ex.Message}");
            }
        }

        private void SaveBlockedDevices()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(BlockedDevicesValue, blockedDevices.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Error saving blocked devices: {ex.Message}");
            }
        }

        private void AddLogEntry(string entry)
        {
            if (listBoxLog.InvokeRequired)
            {
                listBoxLog.Invoke(new Action(() =>
                {
                    listBoxLog.Items.Insert(0, entry);
                    if (listBoxLog.Items.Count > 1000)
                        listBoxLog.Items.RemoveAt(listBoxLog.Items.Count - 1);
                }));
            }
            else
            {
                listBoxLog.Items.Insert(0, entry);
                if (listBoxLog.Items.Count > 1000)
                    listBoxLog.Items.RemoveAt(listBoxLog.Items.Count - 1);
            }
        }

        private string GetCharFromKey(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return key.ToString().ToLower();
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((int)(key - Keys.D0)).ToString();
            if (key == Keys.OemPeriod || key == Keys.Decimal)
                return ".";
            if (key == Keys.Oemcomma)
                return ",";
            if (key == Keys.OemMinus || key == Keys.Subtract)
                return "-";
            if (key == Keys.Space)
                return " ";
            if (key == Keys.OemQuestion || key == Keys.Divide)
                return "/";
            if (key == Keys.OemPipe || key == Keys.OemBackslash)
                return "\\";
            if (key == Keys.Enter)
                return "\n";
            if (key == Keys.Tab)
                return "\t";

            return string.Empty;
        }

        private void UpdateDeviceList()
        {
            var currentTime = DateTime.Now;
            var toRemove = deviceInfos.Where(kvp =>
                (currentTime - kvp.Value.LastActivity).TotalMinutes > 5).Select(kvp => kvp.Key).ToList();

            foreach (var key in toRemove)
            {
                deviceInfos.Remove(key);
            }

            deviceNameCache.Clear();
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            isLogging = !isLogging;

            if (isLogging)
            {
                startBtn.Text = "Stop Logging";
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Logging STARTED");
            }
            else
            {
                startBtn.Text = "Start Logging";
                AddLogEntry($"[{DateTime.Now:HH:mm:ss}] Logging STOPPED");
            }
        }

        private void clearBtn_Click(object sender, EventArgs e)
        {
            listBoxLog.Items.Clear();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UninstallHooks();

            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }

            _keyboardHookProc = null;
            _globalHotkeyHookProc = null;

            if (isLogging)
            {
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                rid[0].usUsagePage = 0x01;
                rid[0].usUsage = 0x06;
                rid[0].dwFlags = RIDEV_REMOVE;
                rid[0].hwndTarget = IntPtr.Zero;

                RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0]));
            }
        }

        #region WinAPI для глобальных хуков

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

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

        #endregion

        #region WinAPI для Raw Input

        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RIDEV_DEVNOTIFY = 0x00002000;
        private const int RIDEV_REMOVE = 0x00000001;
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RID_INPUT = 0x10000003;
        private const int RIDI_DEVICENAME = 0x20000007;
        private const int RI_KEY_BREAK = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;

            [FieldOffset(16)]
            public RAWKEYBOARD data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public ulong ExtraInformation;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand,
            IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand,
            IntPtr pData, ref uint pcbSize);

        #endregion

        private void lstHid_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                HidDevice selDev = gldevices[lstHid.SelectedIndex];
                byte[] bs;
                byte[] bd;
                var desc = selDev.Description;
                var pid = selDev.Attributes.ProductId;
                var vid = selDev.Attributes.VendorId;
                var ver = selDev.Attributes.Version;
                var testmManuf = selDev.ReadManufacturer(out bs);
                var serNum = selDev.ReadSerialNumber(out bd);

                string manuf = "";
                foreach (byte b in bs)
                {
                    if (b > 0)
                        manuf += ((char)b).ToString();
                }

                string ser = "";
                foreach (byte b in bd)
                {
                    if (b > 0)
                        ser += ((char)b).ToString();
                }
                lblInfo.Text = $"Info: \r\n Description: {desc} \r\n PID: {pid} \r\n VID: {vid}\r\n Company: {manuf} \r\n Serial number: {ser} ";
            }
            catch
            {
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            SettingsForm settingsForm = new SettingsForm(this);
            settingsForm.Show();
        }

        public string SavedHotkey { get; set; }

        private void LoadSettings()
        {
            try
            {
                if (!string.IsNullOrEmpty(Properties.Settings.Default.HotkeyCombination))
                {
                    SavedHotkey = Properties.Settings.Default.HotkeyCombination;
                    ParseHotkeyCombination();
                    SetupGlobalHotkeyHook();
                }
            }
            catch
            {

            }
        }

        public void UpdateHotkeyCombination(string newHotkey)
        {
            try
            {
                SavedHotkey = newHotkey;
                Properties.Settings.Default.HotkeyCombination = newHotkey;
                Properties.Settings.Default.Save();

                ParseHotkeyCombination();
                SetupGlobalHotkeyHook();
            }
            catch
            {

            }
        }

    }

    public class DeviceInfo
    {
        public string DeviceId { get; set; }
        public string DevicePath { get; set; }
        public DateTime LastKeyTime { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsBlocked { get; set; }

        public DeviceInfo()
        {
            LastActivity = DateTime.Now;
        }
    }
}
