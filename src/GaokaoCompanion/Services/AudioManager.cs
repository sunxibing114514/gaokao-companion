using System.Windows.Media;
using System.Windows.Threading;

namespace GaokaoCompanion.Services;

/// <summary>
/// 统一音频播放器(句子语音 + 网易云音乐共用)。
/// 不循环播放;Ctrl+Alt+X 调用 Stop() 即可关闭当前声音。
/// </summary>
public class AudioManager
{
    private readonly MediaPlayer _player = new();
    private readonly Dispatcher _uiDispatcher;
    private TimeSpan _seekTarget;
    private bool _seekPending;

    public string StatusText { get; private set; } = "空闲";
    public event Action<string>? StatusChanged;

    /// <summary>播放状态变化:title = 正在播放的标题;null = 停止/结束/失败(用于隐藏「正在播放」小窗)。</summary>
    public event Action<string?>? PlaybackChanged;

    public AudioManager()
    {
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaFailed += (s, e) =>
        {
            SetStatus("播放失败:" + (e.ErrorException?.Message ?? "未知错误"));
            FirePlaybackChanged(null);
        };
        _player.MediaEnded += (s, e) =>
        {
            SetStatus("播放结束");
            FirePlaybackChanged(null);
        };
    }

    public void SetVolume(int percent)
    {
        Safe(() =>
        {
            try { _player.Volume = Math.Clamp(percent, 0, 100) / 100.0; }
            catch (Exception ex) { Logger.Error("设置音量失败", ex); }
        });
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        Safe(() =>
        {
            if (!_seekPending) return;
            _seekPending = false;
            try { _player.Position = _seekTarget; }
            catch (Exception ex) { Logger.Error("定位播放进度失败", ex); }
        });
    }

    /// <summary>播放;startAt 不为空时从该位置开始(歌词定位)。</summary>
    public void Play(string url, string label, TimeSpan? startAt = null)
    {
        Safe(() =>
        {
            try
            {
                _seekPending = false;
                _player.Stop();
                _player.Close();

                if (string.IsNullOrWhiteSpace(url))
                {
                    SetStatus("未获取到音频链接");
                    return;
                }

                if (startAt.HasValue)
                {
                    _seekTarget = startAt.Value;
                    _seekPending = true;
                }

                _player.Open(new Uri(url, UriKind.Absolute));
                _player.Play();

                string seekInfo = startAt.HasValue
                    ? "(从 " + FormatTime(startAt.Value) + " 开始)"
                    : "";
                SetStatus("正在播放:" + label + seekInfo);
                FirePlaybackChanged(label);
            }
            catch (Exception ex)
            {
                SetStatus("播放失败:" + ex.Message);
            }
        });
    }

    public void Stop()
    {
        Safe(() =>
        {
            try
            {
                _seekPending = false;
                _player.Stop();
                _player.Close();
                SetStatus("已停止播放");
                FirePlaybackChanged(null);
            }
            catch (Exception ex)
            {
                Logger.Error("停止播放失败", ex);
            }
        });
    }

    private void Safe(Action action)
    {
        if (_uiDispatcher.CheckAccess()) action();
        else _uiDispatcher.BeginInvoke(action);
    }

    private void FirePlaybackChanged(string? title)
    {
        var handler = PlaybackChanged;
        if (handler == null) return;
        if (_uiDispatcher.CheckAccess()) handler(title);
        else _uiDispatcher.BeginInvoke(() => handler(title));
    }

    private void SetStatus(string text)
    {
        StatusText = text;
        var handler = StatusChanged;
        if (handler == null) return;
        if (_uiDispatcher.CheckAccess()) handler(text);
        else _uiDispatcher.BeginInvoke(() => handler(text));
    }

    private static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
}
