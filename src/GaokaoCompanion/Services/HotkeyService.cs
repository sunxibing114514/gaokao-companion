using System.Runtime.InteropServices;

namespace GaokaoCompanion.Services;

public enum Chord
{
    /// <summary>Ctrl+H+J+S:句子语音搜索</summary>
    SentenceSearch,
    /// <summary>Ctrl+H+J+N:网易云音乐搜索</summary>
    MusicSearch,
    /// <summary>Ctrl+H+J+C:停止播放</summary>
    StopAudio,
}

/// <summary>
/// 全局多键组合热键(WH_KEYBOARD_LL 低级键盘钩子):
/// 当 Ctrl + H + J + S / N / C 同时按下时触发对应动作。
/// </summary>
public class HotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkH = 0x48;
    private const int VkJ = 0x4A;
    private const int VkS = 0x53;
    private const int VkN = 0x4E;
    private const int VkC = 0x43;

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdllHookstruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);

    private readonly LowLevelHookProc _proc;
    private readonly HashSet<int> _down = new();
    private Chord? _lastFired;
    private IntPtr _hook = IntPtr.Zero;

    public event Action<Chord>? ChordTriggered;

    public HotkeyService()
    {
        _proc = HookProc;
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(IntPtr.Zero), 0);
        if (_hook == IntPtr.Zero) Logger.Error("安装键盘钩子失败,快捷键不可用");
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var kb = Marshal.PtrToStructure<KbdllHookstruct>(lParam);
                OnKey((int)kb.VkCode, msg is WmKeyDown or WmSysKeyDown);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsCtrl(int vk) => vk == VkLControl || vk == VkRControl;

    private void OnKey(int vk, bool isDown)
    {
        if (isDown)
        {
            if (!_down.Add(vk)) return; // 忽略按住不放的自动重复
        }
        else
        {
            _down.Remove(vk);
            if (_lastFired.HasValue && BelongsTo(_lastFired.Value, vk)) _lastFired = null;
            return;
        }

        bool ctrl = _down.Contains(VkLControl) || _down.Contains(VkRControl);
        bool h = _down.Contains(VkH);
        bool j = _down.Contains(VkJ);
        if (!ctrl || !h || !j) return;

        TryFire(Chord.SentenceSearch, VkS, _down.Contains(VkS));
        TryFire(Chord.MusicSearch, VkN, _down.Contains(VkN));
        TryFire(Chord.StopAudio, VkC, _down.Contains(VkC));
    }

    private void TryFire(Chord chord, int vk, bool held)
    {
        if (held && _lastFired != chord)
        {
            _lastFired = chord;
            ChordTriggered?.Invoke(chord);
        }
    }

    private static bool BelongsTo(Chord chord, int vk) => chord switch
    {
        Chord.SentenceSearch => vk == VkS || vk == VkH || vk == VkJ || IsCtrl(vk),
        Chord.MusicSearch => vk == VkN || vk == VkH || vk == VkJ || IsCtrl(vk),
        Chord.StopAudio => vk == VkC || vk == VkH || vk == VkJ || IsCtrl(vk),
        _ => false,
    };
}
