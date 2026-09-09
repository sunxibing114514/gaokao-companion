using System.Windows;
using System.Windows.Media;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 搜索框窗口基类:无边框透明、置顶、顶部居中、按配置透明度显示、失焦自动关闭。
/// </summary>
public abstract class LauncherWindow : Window
{
    private bool _closeRequested;

    protected LauncherWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Opacity = Math.Clamp(App.Config.SearchOpacity, 5, 100) / 100.0;
        SourceInitialized += (s, e) => PositionTopCenter();
        Deactivated += (s, e) => TryClose();
    }

    /// <summary>
    /// 失焦自动关闭。必须防重入:窗口关闭过程中激活状态变化会再次触发 Deactivated,
    /// 此时再调 Close() 会抛 InvalidOperationException(“无法在窗口关闭期间…”)导致关不掉。
    /// </summary>
    private void TryClose()
    {
        if (_closeRequested) return;
        _closeRequested = true;
        try
        {
            Close();
        }
        catch
        {
            // 窗口本来就在关闭,忽略任何关闭期异常
        }
    }

    protected void PositionTopCenter()
    {
        double width = Width;
        if (double.IsNaN(width) || width <= 0) width = 620;
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + Math.Max(0, (wa.Width - width) / 2);
        Top = wa.Top + 56;
    }
}
