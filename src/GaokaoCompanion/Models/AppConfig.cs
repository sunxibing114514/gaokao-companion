using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GaokaoCompanion.Models;

/// <summary>
/// 全部本地配置(数据源地址、API 地址、倒计时外观、搜索框透明度、壁纸规则等),
/// 仅保存在 exe 同目录下的 config.json 一个文件中。
/// </summary>
public class AppConfig
{
    public string CountdownJsonUrl { get; set; } = "";
    public string SoundsJsonUrl { get; set; } = "";
    public string NeteaseApiBase { get; set; } = "https://wyyapi.hjymoon.us.ci/";
    public int SentenceIntervalMinutes { get; set; } = 5;
    public double FontSize { get; set; } = 30;
    public double SentenceMaxWidth { get; set; } = 560;
    public double CountdownMaxWidth { get; set; } = 720;
    public int WidgetBackgroundOpacity { get; set; } = 60;
    public bool WidgetTopmost { get; set; } = true;
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }
    public int SearchOpacity { get; set; } = 30;
    public int Volume { get; set; } = 80;
    public List<WallpaperRule> Wallpapers { get; set; } = new();

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppConfig Load()
    {
        string path = ConfigPath;
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions);
                if (loaded != null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                try { File.Copy(path, path + ".corrupt", true); } catch { /* 忽略备份失败 */ }
                Console.Error.WriteLine("配置读取失败,已备份并使用默认配置:" + ex.Message);
            }
        }

        var fresh = new AppConfig();
        fresh.Save();
        return fresh;
    }

    public void Save()
    {
        Normalize();
        string path = ConfigPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public void ReloadFromDisk()
    {
        var fresh = new AppConfig();
        if (File.Exists(ConfigPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions);
                if (loaded != null) fresh = loaded;
            }
            catch { /* 文件损坏时保持默认 */ }
        }
        CopyFrom(fresh);
    }

    public void CopyFrom(AppConfig other)
    {
        CountdownJsonUrl = other.CountdownJsonUrl;
        SoundsJsonUrl = other.SoundsJsonUrl;
        NeteaseApiBase = other.NeteaseApiBase;
        SentenceIntervalMinutes = other.SentenceIntervalMinutes;
        FontSize = other.FontSize;
        SentenceMaxWidth = other.SentenceMaxWidth;
        CountdownMaxWidth = other.CountdownMaxWidth;
        WidgetBackgroundOpacity = other.WidgetBackgroundOpacity;
        WidgetTopmost = other.WidgetTopmost;
        WidgetLeft = other.WidgetLeft;
        WidgetTop = other.WidgetTop;
        SearchOpacity = other.SearchOpacity;
        Volume = other.Volume;
        Wallpapers = new List<WallpaperRule>(other.Wallpapers);
    }

    public void Normalize()
    {
        if (SentenceIntervalMinutes < 1) SentenceIntervalMinutes = 1;
        if (SentenceIntervalMinutes > 1440) SentenceIntervalMinutes = 1440;
        if (FontSize < 10) FontSize = 10;
        if (FontSize > 96) FontSize = 96;
        if (SentenceMaxWidth < 80) SentenceMaxWidth = 80;
        if (SentenceMaxWidth > 2000) SentenceMaxWidth = 2000;
        if (CountdownMaxWidth < 80) CountdownMaxWidth = 80;
        if (CountdownMaxWidth > 3000) CountdownMaxWidth = 3000;
        if (WidgetBackgroundOpacity < 0) WidgetBackgroundOpacity = 0;
        if (WidgetBackgroundOpacity > 100) WidgetBackgroundOpacity = 100;
        if (SearchOpacity < 5) SearchOpacity = 5;
        if (SearchOpacity > 100) SearchOpacity = 100;
        if (Volume < 0) Volume = 0;
        if (Volume > 100) Volume = 100;

        CountdownJsonUrl = CountdownJsonUrl.Trim();
        SoundsJsonUrl = SoundsJsonUrl.Trim();
        NeteaseApiBase = NeteaseApiBase.Trim();
        if (NeteaseApiBase.Length == 0) NeteaseApiBase = "https://wyyapi.hjymoon.us.ci/";

        Wallpapers ??= new List<WallpaperRule>();
        foreach (var rule in Wallpapers)
        {
            if (rule.Day < 0 || rule.Day > 7) rule.Day = 0;
            if (rule.Hour < 0 || rule.Hour > 23) rule.Hour = 0;
            if (rule.Minute < 0 || rule.Minute > 59) rule.Minute = 0;
            rule.ImagePath = rule.ImagePath.Trim();
        }
    }
}
