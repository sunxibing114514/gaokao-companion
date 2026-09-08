using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GaokaoCompanion.Models;
using GaokaoCompanion.Services;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 桌面倒计时组件:可拖拽、长按弹出菜单(可选择关闭)、字体大小可改、
/// 自动换行(换行内容出现在“丨”之后)、句子按配置间隔轮换。
/// </summary>
public partial class WidgetWindow : Window
{
    private CountdownData? _data;
    private int _sentenceIndex = -1;
    private string _lastUrl = "";

    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _sentenceTimer;
    private readonly DispatcherTimer _retryTimer;
    private readonly DispatcherTimer _holdTimer;
    private readonly DispatcherTimer _saveTimer;

    private bool _dragging;
    private bool _holdFired;
    private POINT _cursorStart;
    private double _winStartX;
    private double _winStartY;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    public WidgetWindow()
    {
        InitializeComponent();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _clock.Tick += (s, e) => UpdateCountdownText();

        _sentenceTimer = new DispatcherTimer();
        _sentenceTimer.Tick += (s, e) => NextSentence();

        _retryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _retryTimer.Tick += (s, e) =>
        {
            _retryTimer.Stop();
            LoadData();
        };

        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _holdTimer.Tick += (s, e) =>
        {
            _holdTimer.Stop();
            if (_dragging) return;
            _holdFired = true;
            RootBorder.ReleaseMouseCapture();
            WidgetMenu.IsOpen = true;
        };

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (s, e) =>
        {
            _saveTimer.Stop();
            SavePositionNow();
        };

        ApplyConfig();

        Loaded += (s, e) =>
        {
            RestorePosition();
            _clock.Start();
            LoadData();
        };
    }

    /// <summary>设置保存后由 App 调用,实时应用外观与句子切换间隔。</summary>
    public void ApplyConfig()
    {
        var c = App.Config;
        Topmost = c.WidgetTopmost;
        CountdownText.FontSize = c.FontSize;
        SeparatorText.FontSize = c.FontSize;
        SentenceText.FontSize = Math.Max(10, c.FontSize * 0.8);
        SentenceText.LineHeight = Math.Max(12, c.FontSize * 0.8 * 1.3);
        SentenceText.MaxWidth = c.SentenceMaxWidth;
        CountdownText.MaxWidth = c.CountdownMaxWidth;

        byte alpha = (byte)(Math.Clamp(c.WidgetBackgroundOpacity, 0, 100) * 255 / 100);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x1C, 0x1B, 0x1F));

        _sentenceTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, c.SentenceIntervalMinutes));
        _sentenceTimer.Stop();
        _sentenceTimer.Start();

        if (_lastUrl != c.CountdownJsonUrl)
        {
            _lastUrl = c.CountdownJsonUrl;
            if (IsLoaded) LoadData();
        }
    }

    // ==================== 数据加载与显示 ====================

    private async void LoadData()
    {
        _retryTimer.Stop();
        string url = App.Config.CountdownJsonUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            CountdownText.Text = "未配置倒计时 JSON 地址";
            SentenceText.Text = "长按此处 → 打开设置,填写数据源";
            return;
        }

        CountdownText.Text = "正在加载…";
        SentenceText.Text = "";
        try
        {
            _data = await DataService.FetchCountdownAsync(url);
            _sentenceIndex = -1;
            NextSentence();
            UpdateCountdownText();
        }
        catch (Exception ex)
        {
            Logger.Error("倒计时数据加载失败", ex);
            CountdownText.Text = "倒计时数据加载失败";
            SentenceText.Text = ex.Message + "(60 秒后自动重试)";
            _retryTimer.Start();
        }
    }

    private void UpdateCountdownText()
    {
        if (_data == null) return;
        var target = _data.TargetDate;
        if (target == null)
        {
            CountdownText.Text = "日期格式无效";
            return;
        }

        var now = DateTime.Now;
        if (now >= target.Value)
        {
            CountdownText.Text = "距离" + _data.EventDisplay + "还有0年0月0天0时0分0秒";
            return;
        }

        CountdownText.Text = "距离" + _data.EventDisplay + "还有" + FormatSpan(now, target.Value);
    }

    /// <summary>计算 日历差(年/月/天) + 剩余时分秒。</summary>
    private static string FormatSpan(DateTime now, DateTime target)
    {
        DateTime anchor = now;
        int years = 0;
        while (anchor.AddYears(years + 1) <= target && years < 200) years++;
        anchor = anchor.AddYears(years);

        int months = 0;
        while (anchor.AddMonths(months + 1) <= target && months < 12) months++;
        anchor = anchor.AddMonths(months);

        int days = 0;
        while (anchor.AddDays(days + 1) <= target && days < 31) days++;
        anchor = anchor.AddDays(days);

        var rest = target - anchor;
        return $"{years}年{months}月{days}天{rest.Hours}时{rest.Minutes}分{rest.Seconds}秒";
    }

    private void NextSentence()
    {
        var list = _data?.Sentences;
        if (list == null || list.Count == 0)
        {
            if (_data != null) SentenceText.Text = "(此数据源没有句子)";
            return;
        }
        _sentenceIndex = (_sentenceIndex + 1) % list.Count;
        SentenceText.Text = list[_sentenceIndex];
    }

    // ==================== 拖拽 + 长按菜单 ====================

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            // 双击 = 立即切换下一句(不用 Control.MouseDoubleClick,因为 Border 不是 Control)
            NextSentence();
            return;
        }

        _dragging = false;
        _holdFired = false;
        if (GetCursorPos(out _cursorStart))
        {
            _winStartX = Left;
            _winStartY = Top;
            var t = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;
            _scaleX = t?.M11 is > 0 ? t.M11 : 1.0;
            _scaleY = t?.M22 is > 0 ? t.M22 : 1.0;
            RootBorder.CaptureMouse();
            _holdTimer.Stop();
            _holdTimer.Start();
        }
    }

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _holdFired) return;
        if (!GetCursorPos(out var cur)) return;

        int dx = cur.X - _cursorStart.X;
        int dy = cur.Y - _cursorStart.Y;

        if (!_dragging)
        {
            if (Math.Abs(dx) + Math.Abs(dy) < 6) return;
            _dragging = true;
            _holdTimer.Stop();
        }

        Left = _winStartX + dx / _scaleX;
        Top = _winStartY + dy / _scaleY;
    }

    private void OnRootMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _holdTimer.Stop();
        if (RootBorder.IsMouseCaptured) RootBorder.ReleaseMouseCapture();
        if (_dragging)
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }
        _dragging = false;
        _holdFired = false;
    }

    private void RestorePosition()
    {
        var c = App.Config;
        if (c.WidgetLeft.HasValue && c.WidgetTop.HasValue)
        {
            Left = c.WidgetLeft.Value;
            Top = c.WidgetTop.Value;
        }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + Math.Max(0, (wa.Width - ActualWidth) / 2);
            Top = wa.Top + 120;
        }

        // 防止窗口跑到屏幕外
        double minX = SystemParameters.VirtualScreenLeft;
        double minY = SystemParameters.VirtualScreenTop;
        double maxX = minX + SystemParameters.VirtualScreenWidth - 120;
        double maxY = minY + SystemParameters.VirtualScreenHeight - 80;
        if (Left < minX) Left = minX;
        if (Left > maxX) Left = maxX;
        if (Top < minY) Top = minY;
        if (Top > maxY) Top = maxY;
    }

    public void SavePositionNow()
    {
        try
        {
            App.Config.WidgetLeft = Left;
            App.Config.WidgetTop = Top;
            App.Config.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("保存组件位置失败", ex);
        }
    }

    // ==================== 菜单 ====================

    private void OnNextSentenceClick(object sender, RoutedEventArgs e) => NextSentence();

    private void OnRefreshDataClick(object sender, RoutedEventArgs e) => LoadData();

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e) => App.ShowSettings();

    private void OnCloseWidgetClick(object sender, RoutedEventArgs e)
    {
        SavePositionNow();
        Hide();
    }
}
