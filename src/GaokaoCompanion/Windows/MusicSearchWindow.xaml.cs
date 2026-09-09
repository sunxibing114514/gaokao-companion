using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GaokaoCompanion.Services;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 网易云音乐搜索框(顶部居中、透明度可配置):
/// 输入歌名/歌词自动搜索,↑↓ 选择,回车播放;
/// 歌词命中的结果播放时直接跳到歌词对应位置。
/// </summary>
public partial class MusicSearchWindow : LauncherWindow
{
    private List<NeteaseSong> _results = new();
    private readonly DispatcherTimer _debounce;

    public MusicSearchWindow()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounce.Tick += (s, e) =>
        {
            _debounce.Stop();
            DoSearch();
        };
        App.Audio.StatusChanged += OnAudioStatus;
        Closed += (s, e) => App.Audio.StatusChanged -= OnAudioStatus;
        Loaded += (s, e) =>
        {
            SearchBox.Focus();
        };
    }

    private void OnAudioStatus(string text)
    {
        StatusText.Text = text;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce.Stop();
        if (SearchBox.Text.Trim().Length > 0) _debounce.Start();
    }

    private void OnSearchClick(object sender, RoutedEventArgs e)
    {
        _debounce.Stop();
        DoSearch();
    }

    private async void DoSearch()
    {
        _debounce.Stop();
        string q = SearchBox.Text.Trim();
        if (q.Length == 0)
        {
            ResultList.ItemsSource = null;
            _results = new List<NeteaseSong>();
            StatusText.Text = "输入歌名或歌词,回车搜索";
            return;
        }

        StatusText.Text = "搜索中…";
        ResultList.ItemsSource = null;
        SearchButton.IsEnabled = false;
        try
        {
            _results = await App.Netease.SearchAsync(q);
            ResultList.ItemsSource = _results;
            if (_results.Count == 0)
            {
                StatusText.Text = "没有找到结果,换个关键词试试";
            }
            else
            {
                ResultList.SelectedIndex = 0;
                ResultList.ScrollIntoView(ResultList.SelectedItem);
                StatusText.Text = "找到 " + _results.Count + " 个结果 · ↑↓ 选择 · 回车播放 · 带「词」为歌词命中";
                ResultList.Focus();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "搜索失败:" + ex.Message;
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            TryClose(); // 安全关闭(防重入),不要直接 Close()
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            _debounce.Stop();
            DoSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            MoveSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (SearchBox.IsKeyboardFocused && ResultList.Items.Count == 0)
            {
                _debounce.Stop();
                DoSearch();
            }
            else
            {
                PlaySelected();
            }
            e.Handled = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (ResultList.Items.Count == 0) return;
        int idx = ResultList.SelectedIndex + delta;
        if (idx < 0) idx = 0;
        if (idx >= ResultList.Items.Count) idx = ResultList.Items.Count - 1;
        ResultList.SelectedIndex = idx;
        ResultList.ScrollIntoView(ResultList.SelectedItem);
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        PlaySelected();
    }

    private async void PlaySelected()
    {
        if (ResultList.SelectedItem is not NeteaseSong song) return;

        StatusText.Text = "正在获取《" + song.Name + "》播放链接…";
        try
        {
            string? url = await App.Netease.GetSongUrlAsync(song.Id);
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText.Text = "无法获取《" + song.Name + "》的播放链接(可能需要 VIP 或接口无版权)";
                return;
            }

            TimeSpan? seek = null;
            if (song.FromLyric)
            {
                string keyword = SearchBox.Text.Trim();
                seek = await App.Netease.FindLyricTimeAsync(song.Id, keyword);
                if (seek == null) StatusText.Text = "未在歌词中定位到该句,从头播放…";
            }

            string label = song.Artist.Length > 0 ? song.Name + " - " + song.Artist : song.Name;
            App.Audio.Play(url, label, seek);
        }
        catch (Exception ex)
        {
            StatusText.Text = "播放失败:" + ex.Message;
        }
    }
}
