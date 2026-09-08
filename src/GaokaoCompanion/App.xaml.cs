using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GaokaoCompanion.Models;
using GaokaoCompanion.Services;
using GaokaoCompanion.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace GaokaoCompanion;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static HotkeyService? _hotkeys;
    private static WallpaperService? _wallpapers;
    private static TaskbarIcon? _tray;
    private static WidgetWindow? _widget;
    private static SentenceSearchWindow? _sentenceSearch;
    private static MusicSearchWindow? _musicSearch;
    private static SettingsWindow? _settings;

    public static AppConfig Config { get; private set; } = new();
    public static AudioManager Audio { get; private set; } = new();
    public static NeteaseClient Netease { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "GaokaoCompanion_SingleInstance_E3B90C", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("高考倒计时伴侣已经在运行了。(可在系统托盘找到它)", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Logger.Info("========== 应用启动 ==========");

        try
        {
            Config = AppConfig.Load();
        }
        catch (Exception ex)
        {
            Logger.Error("加载配置失败,使用默认配置", ex);
            Config = new AppConfig();
        }

        Audio = new AudioManager();
        Netease = new NeteaseClient();

        _widget = new WidgetWindow();
        _widget.Show();

        _hotkeys = new HotkeyService();
        _hotkeys.ChordTriggered += OnChordTriggered;
        _hotkeys.Install();

        _wallpapers = new WallpaperService(Config);
        _wallpapers.Start();

        InitTray();
        ApplyConfigChanges();
        Logger.Info("启动完成");
    }

    private void OnChordTriggered(Chord chord)
    {
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                switch (chord)
                {
                    case Chord.SentenceSearch:
                        ShowSentenceSearch();
                        break;
                    case Chord.MusicSearch:
                        ShowMusicSearch();
                        break;
                    case Chord.StopAudio:
                        Audio.Stop();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("处理快捷键失败", ex);
            }
        });
    }

    private static void ShowSentenceSearch()
    {
        if (_sentenceSearch == null)
        {
            _sentenceSearch = new SentenceSearchWindow();
            _sentenceSearch.Closed += (s, e) => _sentenceSearch = null;
            _sentenceSearch.Show();
        }
        else
        {
            _sentenceSearch.Activate();
        }
    }

    private static void ShowMusicSearch()
    {
        if (_musicSearch == null)
        {
            _musicSearch = new MusicSearchWindow();
            _musicSearch.Closed += (s, e) => _musicSearch = null;
            _musicSearch.Show();
        }
        else
        {
            _musicSearch.Activate();
        }
    }

    public static void ShowSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (s, e) => _settings = null;
            _settings.Show();
        }
        else
        {
            _settings.Activate();
        }
    }

    public static void ToggleWidget()
    {
        if (_widget == null) return;
        if (_widget.IsVisible) _widget.Hide();
        else _widget.Show();
    }

    private void InitTray()
    {
        _tray = new TaskbarIcon { ToolTipText = "高考倒计时伴侣" };
        try
        {
            _tray.IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch (Exception ex)
        {
            Logger.Error("托盘图标加载失败", ex);
        }

        var menu = new ContextMenu();
        var toggle = new MenuItem { Header = "显示 / 隐藏倒计时" };
        toggle.Click += (s, e) => ToggleWidget();
        var settings = new MenuItem { Header = "设置…" };
        settings.Click += (s, e) => ShowSettings();
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (s, e) => Shutdown();
        menu.Items.Add(toggle);
        menu.Items.Add(settings);
        menu.Items.Add(exit);
        _tray.ContextMenu = menu;
        _tray.TrayLeftMouseUp += (s, e) => ToggleWidget();
    }

    public static void ApplyConfigChanges()
    {
        try
        {
            Netease.Base = Config.NeteaseApiBase;
            Audio.SetVolume(Config.Volume);
            _widget?.ApplyConfig();
            _wallpapers?.Restart();
        }
        catch (Exception ex)
        {
            Logger.Error("应用配置失败", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("未处理异常", e.Exception);
        MessageBox.Show("发生错误:" + e.Exception.Message, "高考倒计时伴侣",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _widget?.SavePositionNow();
            Config.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("退出时保存配置失败", ex);
        }

        try
        {
            _hotkeys?.Dispose();
            _tray?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error("释放资源失败", ex);
        }

        Logger.Info("应用退出");
        base.OnExit(e);
    }
}
