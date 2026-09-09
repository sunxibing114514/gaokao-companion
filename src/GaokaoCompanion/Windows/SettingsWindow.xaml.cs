using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GaokaoCompanion.Models;
using Microsoft.Win32;

namespace GaokaoCompanion.Windows;

/// <summary>设置窗口:数据源、外观、透明度、壁纸规则,全部保存在 exe 同目录 config.json。</summary>
public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<WallpaperRule> _rules;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = App.Config;
        _rules = new ObservableCollection<WallpaperRule>(App.Config.Wallpapers);
        RulesList.ItemsSource = _rules;
        ConfigPathText.Text = "配置文件:" + AppConfig.ConfigPath;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // 未点保存直接关闭 → 放弃修改,恢复为磁盘上的配置;任何异常都不能阻止窗口关闭
        try { App.Config.ReloadFromDisk(); }
        catch { /* 忽略,保证能关闭 */ }
        Close();
    }

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        _rules.Add(new WallpaperRule { Day = 0, Hour = 8, Minute = 0, ImagePath = "" });
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WallpaperRule rule)
        {
            _rules.Remove(rule);
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WallpaperRule rule) return;

        var dlg = new OpenFileDialog
        {
            Title = "选择壁纸图片",
            Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
        };
        if (dlg.ShowDialog(this) == true)
        {
            rule.ImagePath = dlg.FileName;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Config.Wallpapers = _rules.ToList();
            // 先让设置立即生效(含网易云 Cookie),再持久化——
            // 这样即使写文件失败,本次会话的功能也不受影响
            App.ApplyConfigChanges();

            try
            {
                App.Config.Save();
                string msg = "已保存并应用:" + AppConfig.ConfigPath;
                ConfigPathText.Text = msg;
                MessageBox.Show(this, msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception saveEx)
            {
                string warn = "设置已生效,但写入配置文件失败(下次启动会丢失):\n" + saveEx.Message;
                ConfigPathText.Text = warn;
                MessageBox.Show(this, warn, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "应用设置失败:" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
