using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GaokaoCompanion.Services;

public enum Chord
{
    /// <summary>Ctrl+Alt+S:句子语音搜索</summary>
    SentenceSearch,
    /// <summary>Ctrl+Alt+N:网易云音乐搜索</summary>
    MusicSearch,
    /// <summary>Ctrl+Alt+X:停止播放</summary>
    StopAudio,
}

/// <summary>
/// 全局组合热键(WH_KEYBOARD_LL 低级键盘钩子,只监听不拦截,不影响其它软件):
/// Ctrl+Alt+S = 句子语音 / Ctrl+Alt+N = 网易云音乐 / Ctrl+Alt+X = 停止播放。
/// 冲突规避:要求 exactly Ctrl+Alt(按住 Shift 或 Win 时不触发,避免误触);
/// 刻意避开系统保留键(Win 系列、Alt+Tab)与高频应用组合(Ctrl+Shift+*、Alt+单键);
/// 停止键用 X 而非 C(Ctrl+Alt+C 被截图/OCR 类工具占用的概率更高)。
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
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkS = 0x53;
    private const int VkN = 0x4E;
    private const int VkX = 0x58;

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
    private static bool IsAlt(int vk) => vk == VkLMenu || vk == VkRMenu;

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
        bool alt = _down.Contains(VkLMenu) || _down.Contains(VkRMenu);
        bool shift = _down.Contains(VkLShift) || _down.Contains(VkRShift);
        bool win = _down.Contains(VkLWin) || _down.Contains(VkRWin);
        if (!ctrl || !alt || shift || win) return; // 仅响应 Ctrl+Alt(+字母)

        TryFire(Chord.SentenceSearch, VkS, _down.Contains(VkS));
        TryFire(Chord.MusicSearch, VkN, _down.Contains(VkN));
        TryFire(Chord.StopAudio, VkX, _down.Contains(VkX));
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
        Chord.SentenceSearch => vk == VkS || IsCtrl(vk) || IsAlt(vk),
        Chord.MusicSearch => vk == VkN || IsCtrl(vk) || IsAlt(vk),
        Chord.StopAudio => vk == VkX || IsCtrl(vk) || IsAlt(vk),
        _ => false,
    };
}
