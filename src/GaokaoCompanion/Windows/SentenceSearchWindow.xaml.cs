using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GaokaoCompanion.Services;

namespace GaokaoCompanion.Windows;

/// <summary>
/// 句子语音搜索框(顶部居中、透明度可配置):输入筛选句子,
/// ↑↓ 选择,回车/双击播放对应声音(不循环)。Esc 关闭,F5 重载。
/// </summary>
public partial class SentenceSearchWindow : LauncherWindow
{
    private Dictionary<string, string> _sounds = new();
    private List<string> _allKeys = new();

    public SentenceSearchWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        App.Audio.StatusChanged += OnAudioStatus;
        Closed += (s, e) => App.Audio.StatusChanged -= OnAudioStatus;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        _ = LoadSoundsAsync();
    }

    private async Task LoadSoundsAsync()
    {
        string url = App.Config.DataJsonUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusText.Text = "未配置句子语音 JSON 地址(托盘 → 设置)";
            return;
        }

        StatusText.Text = "正在加载句子数据…";
        try
        {
            _sounds = await DataService.FetchSoundsAsync(url);
            _allKeys = _sounds.Keys.ToList();
            Filter();
            StatusText.Text = _allKeys.Count == 0
                ? "句子数据为空"
                : "共 " + _allKeys.Count + " 句 · 输入筛选 · ↑↓ 选择 · 回车播放 · Esc 关闭";
        }
        catch (Exception ex)
        {
            StatusText.Text = "加载失败:" + ex.Message + "(按 F5 重试)";
        }
    }

    private void OnAudioStatus(string text)
    {
        StatusText.Text = text;
    }

    private void Filter()
    {
        string q = SearchBox.Text.Trim();
        var list = q.Length == 0
            ? _allKeys
            : _allKeys.Where(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ResultList.ItemsSource = list;
        if (list.Count > 0)
        {
            ResultList.SelectedIndex = 0;
            ResultList.ScrollIntoView(ResultList.SelectedItem);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        Filter();
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
            StatusText.Text = "重新加载…";
            _ = LoadSoundsAsync();
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
            PlaySelected();
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

    private void PlaySelected()
    {
        if (ResultList.SelectedItem is not string sentence) return;
        if (!_sounds.TryGetValue(sentence, out var url))
        {
            StatusText.Text = "未找到该句子的声音链接";
            return;
        }
        App.Audio.Play(url, sentence);
        TryClose(); // 已开始播放 → 搜索框关闭,右下角弹出「正在播放」小窗
    }
}
