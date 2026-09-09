using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 「正在播放」小窗(M3 毛玻璃):播放开始时在屏幕右下角弹出,显示歌曲/句子,
/// 8 秒后自动淡出;点击立即关闭;停止/失败/播完立即隐藏。
/// 不抢焦点(ShowActivated=False、Focusable=False),不影响输入。
/// 毛玻璃:优先 Win11 DWM Acrylic Backdrop,回退 Win10 SetWindowCompositionAttribute,
/// 再回退半透明底色。
/// </summary>
public partial class NowPlayingWindow : Window
{
    private const int HideAfterSeconds = 8;

    private readonly DispatcherTimer _hideTimer;
    private bool _fadingOut;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    public NowPlayingWindow()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(HideAfterSeconds) };
        _hideTimer.Tick += (s, e) =>
        {
            _hideTimer.Stop();
            HideToast();
        };
        SizeChanged += (s, e) => PositionAtBottomRight();
        SourceInitialized += (s, e) => TryEnableAcrylic();
    }

    /// <summary>显示/更新播放标题并重置自动隐藏计时。</summary>
    public void Notify(string title)
    {
        TitleText.Text = title;
        PositionAtBottomRight();
        _fadingOut = false;
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }
        AnimateOpacity(1);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    /// <summary>停止/失败/播完时立即隐藏。</summary>
    public void HideToast()
    {
        _hideTimer.Stop();
        if (!IsVisible || _fadingOut) return;
        _fadingOut = true;
        AnimateOpacity(0, onCompleted: () =>
        {
            Hide();
            _fadingOut = false;
        });
    }

    private void OnDismiss(object sender, System.Windows.Input.MouseButtonEventArgs e) => HideToast();

    private void AnimateOpacity(double to, Action? onCompleted = null)
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(220),
        };
        if (onCompleted != null) anim.Completed += (s, e) => onCompleted();
        BeginAnimation(OpacityProperty, anim);
    }

    private void PositionAtBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        double h = double.IsNaN(ActualHeight) || ActualHeight < 1 ? 100 : ActualHeight;
        Left = wa.Right - Width - 26;
        Top = wa.Bottom - h - 26;
    }

    /// <summary>启用系统级毛玻璃:Win11 Acrylic Backdrop → Win10 Acrylic → 半透明底色兜底。</summary>
    private void TryEnableAcrylic()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // 深色模式混合(Win10 1809+/Win11)
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE

            // 1) Win11 22H2+: DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_ACRYLIC
            int acrylic = 3;
            if (DwmSetWindowAttribute(hwnd, 38, ref acrylic, 4) == 0)
            {
                Root.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x14, 0x12, 0x18));
                return;
            }

            // 2) Win10: SetWindowCompositionAttribute + ACRYLICBLURBEHIND
            var accent = new AccentPolicy
            {
                AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                // ABGR: alpha 0x66 + #141218
                GradientColor = 0x66181214,
            };
            int size = Marshal.SizeOf<AccentPolicy>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    Data = ptr,
                    SizeOfData = size,
                };
                if (SetWindowCompositionAttribute(hwnd, ref data) != 0)
                {
                    Root.Background = new SolidColorBrush(Color.FromArgb(0x80, 0x14, 0x12, 0x18));
                    return;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            // 任何失败都落到下面的半透明底色
        }

        // 3) 兜底:保持 XAML 里的半透明底色,不做系统模糊
    }
}
