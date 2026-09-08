using System.Windows;
using System.Windows.Media;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 搜索框窗口基类:无边框透明、置顶、顶部居中、按配置透明度显示、失焦自动关闭。
/// </summary>
public abstract class LauncherWindow : Window
{
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
        Deactivated += (s, e) => Close();
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
