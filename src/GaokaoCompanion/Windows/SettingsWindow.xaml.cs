using System.Collections.ObjectModel;
using System.ComponentModel;
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
        // 未点保存直接关闭 → 放弃修改,恢复为磁盘上的配置
        App.Config.ReloadFromDisk();
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
            App.Config.Save();
            App.ApplyConfigChanges();
            ConfigPathText.Text = "已保存:" + AppConfig.ConfigPath;
            MessageBox.Show(this, "已保存并应用。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败:" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
