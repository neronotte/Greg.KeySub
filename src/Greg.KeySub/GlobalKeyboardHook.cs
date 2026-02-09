using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Greg.KeySub;

/// <summary>
/// Global low-level keyboard hook that intercepts keystrokes system-wide.
/// </summary>
public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    // Flag to mark our own injected input
    private static readonly UIntPtr INJECTED_FLAG = unchecked((UIntPtr)0xDEADBEEF);

    private readonly LowLevelKeyboardProc _proc;
    private readonly SynchronizationContext? _syncContext;
    private IntPtr _hookId = IntPtr.Zero;
    private GCHandle _procHandle; // Prevent delegate from being garbage collected
    private bool _disposed;
    private HashSet<int>? _sectionSignKeys; // Cache of VK codes that produce §

    public event EventHandler<KeyInterceptedEventArgs>? KeyIntercepted;
    
    /// <summary>
    /// Diagnostic info about detected § keys
    /// </summary>
    public string DiagnosticInfo { get; private set; } = "";

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
        _procHandle = GCHandle.Alloc(_proc); // Keep delegate alive
        _syncContext = SynchronizationContext.Current;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;

        // Detect which keys produce § on this keyboard layout
        DetectSectionSignKeys();

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);

        if (_hookId == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install keyboard hook. Error code: {errorCode}");
        }
    }

    private void DetectSectionSignKeys()
    {
        // Detect which virtual key codes produce § on the current keyboard layout
        _sectionSignKeys = new HashSet<int>();
        var diagLines = new List<string>();
        
        // Scan all OEM keys and extended range (0x20-0xFF covers most keys)
        byte[] emptyState = new byte[256];
        byte[] shiftState = new byte[256];
        shiftState[0x10] = 0x80; // VK_SHIFT
        shiftState[0xA0] = 0x80; // VK_LSHIFT

        for (int vk = 0x20; vk <= 0xFF; vk++)
        {
            string? charNoShift = GetCharacterForKey(vk, emptyState);
            string? charWithShift = GetCharacterForKey(vk, shiftState);
            
            bool producesSection = charNoShift == "§" || charWithShift == "§";
            
            if (producesSection)
            {
                _sectionSignKeys.Add(vk);
                diagLines.Add($"Found § on VK 0x{vk:X2}: normal='{charNoShift ?? ""}' shift='{charWithShift ?? ""}'");
            }
        }

        DiagnosticInfo = _sectionSignKeys.Count > 0 
            ? string.Join("\n", diagLines)
            : "No keys found that produce §. Will monitor all OEM keys.";

        // If no keys found, monitor all OEM keys as fallback
        if (_sectionSignKeys.Count == 0)
        {
            for (int vk = 0xBA; vk <= 0xE2; vk++)
            {
                _sectionSignKeys.Add(vk);
            }
        }
    }

    private static string? GetCharacterForKey(int vkCode, byte[] keyState)
    {
        var buffer = new System.Text.StringBuilder(4);
        uint scanCode = MapVirtualKey((uint)vkCode, 0);
        
        // Call ToUnicode twice to handle dead keys properly
        ToUnicode((uint)vkCode, scanCode, keyState, buffer, buffer.Capacity, 0);
        buffer.Clear();
        int result = ToUnicode((uint)vkCode, scanCode, keyState, buffer, buffer.Capacity, 0);
        
        return result > 0 && buffer.Length > 0 ? buffer[0].ToString() : null;
    }

    private static bool ProducesCharacter(int vkCode, byte[] keyState, char target)
    {
        var buffer = new System.Text.StringBuilder(4);
        uint scanCode = MapVirtualKey((uint)vkCode, 0);
        
        // Call ToUnicode twice to handle dead keys properly
        ToUnicode((uint)vkCode, scanCode, keyState, buffer, buffer.Capacity, 0);
        buffer.Clear();
        int result = ToUnicode((uint)vkCode, scanCode, keyState, buffer, buffer.Capacity, 0);
        
        return result > 0 && buffer.Length > 0 && buffer[0] == target;
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                
                // Ignore our own injected input
                if (hookStruct.dwExtraInfo == INJECTED_FLAG)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                int vkCode = (int)hookStruct.vkCode;

                // Fast check: is this a potential § key?
                if (_sectionSignKeys != null && _sectionSignKeys.Contains(vkCode))
                {
                    // Check modifier states using GetAsyncKeyState (more reliable in hooks)
                    bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                    bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                    bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;

                    // Skip if Ctrl or Alt is pressed (likely a shortcut)
                    if (!ctrl && !alt)
                    {
                        // Build keyboard state with current shift state
                        byte[] keyState = new byte[256];
                        if (shift)
                        {
                            keyState[0x10] = 0x80; // VK_SHIFT
                            keyState[0xA0] = 0x80; // VK_LSHIFT
                        }
                        
                        if (ProducesCharacter(vkCode, keyState, '§'))
                        {
                            var args = new KeyInterceptedEventArgs(vkCode);
                            KeyIntercepted?.Invoke(this, args);

                            if (args.Handled)
                            {
                                // Send the replacement character directly
                                SendReplacementKey(args.ReplacementChar);
                                
                                return (IntPtr)1; // Block the original key
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Never let exceptions escape from the hook callback
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static void SendReplacementKey(char replacement)
    {
        var inputs = new INPUT[2];

        // Key down
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0;
        inputs[0].u.ki.wScan = (ushort)replacement;
        inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
        inputs[0].u.ki.time = 0;
        inputs[0].u.ki.dwExtraInfo = INJECTED_FLAG;

        // Key up
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0;
        inputs[1].u.ki.wScan = (ushort)replacement;
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        inputs[1].u.ki.time = 0;
        inputs[1].u.ki.dwExtraInfo = INJECTED_FLAG;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Uninstall();
            if (_procHandle.IsAllocated)
            {
                _procHandle.Free();
            }
            _disposed = true;
        }
    }

    #region Native Methods

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int INPUT_KEYBOARD = 1;
    private const int KEYEVENTF_UNICODE = 0x0004;
    private const int KEYEVENTF_KEYUP = 0x0002;

    // INPUT structure for SendInput - must match Windows API exactly
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    // MOUSEINPUT is larger, needed for correct union size
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    #endregion
}

public class KeyInterceptedEventArgs : EventArgs
{
    public int VirtualKeyCode { get; }
    public bool Handled { get; set; }
    public char ReplacementChar { get; set; } = '`';

    public KeyInterceptedEventArgs(int virtualKeyCode)
    {
        VirtualKeyCode = virtualKeyCode;
        Handled = true; // Default to handling (replacing) the key
    }
}
