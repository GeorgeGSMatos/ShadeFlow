using System.ComponentModel;
using System.Runtime.InteropServices;
using ShadeFlow.Models;

namespace ShadeFlow.Services;

/// <summary>Short-lived capture limited to the editor's foreground window. No keystroke history is retained.</summary>
internal sealed class ForegroundShortcutCapture : IDisposable
{
    private const int KeyboardLowLevelHook = 13;
    private const int KeyDown = 0x0100;
    private const int KeyUp = 0x0101;
    private const int SystemKeyDown = 0x0104;
    private const int SystemKeyUp = 0x0105;
    private const uint EscapeKey = 0x1B;
    private static readonly uint[] ModifierKeys = [0x5B, 0x5C, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5];
    private readonly HashSet<uint> _pressed = [];
    private readonly nint _owner;
    private readonly Action<HotkeyBinding> _accept;
    private readonly Action<uint> _modifiersChanged;
    private readonly Action _cancel;
    private readonly Action<Exception> _failed;
    private readonly HookCallback _callback;
    private GCHandle _callbackRoot;
    private nint _hook;
    private bool _disposed;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
    private delegate nint HookCallback(int code, nint message, nint data);

    public ForegroundShortcutCapture(nint owner, Action<HotkeyBinding> accept, Action<uint> modifiersChanged,
        Action cancel, Action<Exception> failed)
    {
        if (owner == 0) throw new ArgumentException("A janela de captura ainda não está disponível.", nameof(owner));
        _owner = owner;
        _accept = accept;
        _modifiersChanged = modifiersChanged;
        _cancel = cancel;
        _failed = failed;
        foreach (var key in ModifierKeys) if ((GetAsyncKeyState((int)key) & 0x8000) != 0) _pressed.Add(key);
        _callback = Receive;
        _callbackRoot = GCHandle.Alloc(_callback);
        _hook = SetWindowsHookEx(KeyboardLowLevelHook, _callback, GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            _callbackRoot.Free();
            throw new InvalidOperationException("Não foi possível iniciar a captura das teclas.", error);
        }
    }

    private nint Receive(int code, nint message, nint data)
    {
        if (code < 0 || _disposed || GetForegroundWindow() != _owner)
            return CallNextHookEx(_hook, code, message, data);
        var kind = (int)message;
        if (kind is not (KeyDown or KeyUp or SystemKeyDown or SystemKeyUp))
            return CallNextHookEx(_hook, code, message, data);
        try
        {
            var input = Marshal.PtrToStructure<KeyboardInput>(data);
            var key = NormalizeModifier(input);
            var down = kind is KeyDown or SystemKeyDown;
            var firstPress = down ? _pressed.Add(key) : _pressed.Remove(key);
            if (ModifierKeys.Contains(key))
            {
                _modifiersChanged(CurrentModifiers());
                // Modifier events must reach Windows so Ctrl/Alt/Shift/Win cannot get stuck.
                return CallNextHookEx(_hook, code, message, data);
            }
            if (!down && !firstPress) return CallNextHookEx(_hook, code, message, data);
            if (down && firstPress)
            {
                if (key == EscapeKey) _cancel();
                else _accept(new HotkeyBinding(CurrentModifiers(), key));
            }
            // Consume the main key before RegisterHotKey in another process can execute it.
            return 1;
        }
        catch (Exception error)
        {
            Dispose();
            _failed(error);
            return CallNextHookEx(0, code, message, data);
        }
    }

    private static uint NormalizeModifier(KeyboardInput input) => input.VirtualKey switch
    {
        0x10 => input.ScanCode == 0x36 ? 0xA1u : 0xA0u,
        0x11 => (input.Flags & 1) != 0 ? 0xA3u : 0xA2u,
        0x12 => (input.Flags & 1) != 0 ? 0xA5u : 0xA4u,
        _ => input.VirtualKey
    };

    private uint CurrentModifiers()
    {
        uint flags = 0;
        if (_pressed.Overlaps([0x12u, 0xA4u, 0xA5u])) flags |= 1;
        if (_pressed.Overlaps([0x11u, 0xA2u, 0xA3u])) flags |= 2;
        if (_pressed.Overlaps([0x10u, 0xA0u, 0xA1u])) flags |= 4;
        if (_pressed.Overlaps([0x5Bu, 0x5Cu])) flags |= 8;
        return flags;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != 0 && !UnhookWindowsHookEx(_hook))
        {
            // Keep the callback rooted if removal failed; it now only forwards events.
            LogService.Current?.Error("Failed to remove the temporary shortcut capture hook.", new Win32Exception(Marshal.GetLastWin32Error()));
            return;
        }
        _hook = 0;
        if (_callbackRoot.IsAllocated) _callbackRoot.Free();
        _pressed.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hook, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? name);
}
