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
        // 关闭一开始就置位守卫:此后任何 Close()/TryClose() 都不会再重入
        //(修复:Esc 直接 Close() 后,关闭过程中的失焦再次触发 Close →
        // InvalidOperationException → 错误弹窗反复出现“关不掉”)
        Closing += (s, e) => _closeRequested = true;
    }

    /// <summary>
    /// 唯一的安全关闭入口(Esc、失焦都走这里):防重入 + 吞掉关闭期异常。
    /// </summary>
    protected void TryClose()
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
