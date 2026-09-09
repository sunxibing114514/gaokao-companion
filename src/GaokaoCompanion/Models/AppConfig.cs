using System.Collections.Generic;
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
    /// <summary>唯一的数据 JSON 地址:倒计时数据 + 句子音效映射共用一个文件(文件内两个对象可并列)。</summary>
    public string DataJsonUrl { get; set; } = "";
    public string NeteaseApiBase { get; set; } = "https://wyyapi.hjymoon.us.ci/";

    /// <summary>网易云 Cookie(如 MUSIC_U=xxx;…),可解锁 VIP 歌曲;留空匿名访问。</summary>
    public string NeteaseCookie { get; set; } = "";
    public int SentenceIntervalMinutes { get; set; } = 5;
    public double FontSize { get; set; } = 30;
    public double SentenceMaxWidth { get; set; } = 560;
    public double CountdownMaxWidth { get; set; } = 720;
    /// <summary>组件背景不透明度 0-100;0 = 完全透明(纯文字悬浮),100 = 不透明卡片。</summary>
    public int WidgetBackgroundOpacity { get; set; } = 0;
    /// <summary>组件文字颜色(如 "#000000");留空 = 跟随数据 JSON 的 color 字段;都为空 = 默认黑色。</summary>
    public string WidgetTextColor { get; set; } = "";
    public bool WidgetTopmost { get; set; } = true;
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }
    public int SearchOpacity { get; set; } = 30;
    public int Volume { get; set; } = 80;
    public List<WallpaperRule> Wallpapers { get; set; } = new();

    private static string? _configPath;

    /// <summary>当前生效的配置文件路径:优先 exe 同目录;不可写时自动回退 %APPDATA%\GaokaoCompanion\config.json。</summary>
    public static string ConfigPath => _configPath ??= ResolveConfigPath();

    private static string GetExeConfigPath() => Path.Combine(AppContext.BaseDirectory, "config.json");

    private static string GetAppDataConfigPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GaokaoCompanion", "config.json");

    private static bool IsDirWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".write_probe");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    private static string ResolveConfigPath()
    {
        string exePath = GetExeConfigPath();
        string appDataPath = GetAppDataConfigPath();
        bool exeExists = File.Exists(exePath);
        bool appExists = File.Exists(appDataPath);

        if (exeExists && appExists)
        {
            // 两边都有 → 取最近修改的(回退迁移后 AppData 通常更新)
            return File.GetLastWriteTimeUtc(appDataPath) > File.GetLastWriteTimeUtc(exePath)
                ? appDataPath
                : exePath;
        }
        if (appExists) return appDataPath;
        if (exeExists) return exePath; // 存在即可读;写入失败由 Save() 内部自愈
        return IsDirWritable(Path.GetDirectoryName(exePath)!) ? exePath : appDataPath;
    }

    private static void TryClearReadOnly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                FileAttributes attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch { /* 尽力而为 */ }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>统一解析配置 JSON,并迁移旧版双字段(countdownJsonUrl/soundsJsonUrl → dataJsonUrl)。</summary>
    private static AppConfig? ParseConfigJson(string text)
    {
        AppConfig? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<AppConfig>(text, JsonOptions);
        }
        catch
        {
            return null;
        }
        if (cfg == null) return null;

        if (string.IsNullOrWhiteSpace(cfg.DataJsonUrl))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    string? legacy = null;
                    if (root.TryGetProperty("countdownJsonUrl", out var c) && c.ValueKind == JsonValueKind.String)
                        legacy = c.GetString();
                    if (string.IsNullOrWhiteSpace(legacy) &&
                        root.TryGetProperty("soundsJsonUrl", out var s) && s.ValueKind == JsonValueKind.String)
                        legacy = s.GetString();
                    if (!string.IsNullOrWhiteSpace(legacy))
                        cfg.DataJsonUrl = legacy!.Trim();
                }
            }
            catch { /* 迁移失败忽略 */ }
        }

        cfg.Normalize();
        return cfg;
    }

    public static AppConfig Load()
    {
        string path = ConfigPath;
        if (File.Exists(path))
        {
            try
            {
                string text = File.ReadAllText(path);
                var loaded = ParseConfigJson(text);
                if (loaded != null)
                {
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                try { File.Copy(path, path + ".corrupt", true); } catch { /* 忽略备份失败 */ }
                Console.Error.WriteLine("配置读取失败,已备份并使用默认配置:" + ex.Message);
            }
        }

        // 只在配置文件不存在时生成(首次运行);已存在则直接使用,不做任何改写
        var fresh = new AppConfig();
        fresh.Save();
        Console.Error.WriteLine("首次运行:已生成配置文件 " + path);
        return fresh;
    }

    public void Save()
    {
        Normalize();
        try
        {
            SaveTo(ConfigPath);
        }
        catch (UnauthorizedAccessException)
        {
            SaveWithRecovery();
        }
        catch (IOException)
        {
            SaveWithRecovery();
        }
    }

    /// <summary>保存失败自愈:清只读属性重试 → 仍失败则自动回退 %APPDATA% 并迁移。</summary>
    private void SaveWithRecovery()
    {
        string path = ConfigPath;
        TryClearReadOnly(path);
        try
        {
            SaveTo(path);
            return;
        }
        catch { /* 继续回退 */ }

        string fallback = GetAppDataConfigPath();
        SaveTo(fallback);          // 仍失败则把真实异常抛给调用方
        _configPath = fallback;
    }

    private void SaveTo(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public static AppConfig? LoadFromDisk()
    {
        try
        {
            string path = ConfigPath;
            if (!File.Exists(path)) return null;
            return ParseConfigJson(File.ReadAllText(path));
        }
        catch { return null; }
    }

    /// <summary>
    /// 合并保存组件位置:先读磁盘当前内容(可能被用户手工编辑过),仅回写位置两个字段。
    /// 避免用内存配置整体覆盖磁盘、抹掉用户对 config.json 的手工修改。
    /// </summary>
    public void SavePositionMerge(double? left, double? top)
    {
        var target = LoadFromDisk() ?? this;
        target.WidgetLeft = left;
        target.WidgetTop = top;
        target.Normalize();
        target.Save();
    }

    public void ReloadFromDisk()
    {
        var fresh = LoadFromDisk() ?? new AppConfig();
        CopyFrom(fresh);
    }

    public void CopyFrom(AppConfig other)
    {
        DataJsonUrl = other.DataJsonUrl;
        NeteaseApiBase = other.NeteaseApiBase;
        NeteaseCookie = other.NeteaseCookie;
        SentenceIntervalMinutes = other.SentenceIntervalMinutes;
        FontSize = other.FontSize;
        SentenceMaxWidth = other.SentenceMaxWidth;
        CountdownMaxWidth = other.CountdownMaxWidth;
        WidgetBackgroundOpacity = other.WidgetBackgroundOpacity;
        WidgetTextColor = other.WidgetTextColor;
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
        WidgetTextColor = WidgetTextColor.Trim();
        if (SearchOpacity < 5) SearchOpacity = 5;
        if (SearchOpacity > 100) SearchOpacity = 100;
        if (Volume < 0) Volume = 0;
        if (Volume > 100) Volume = 100;

        DataJsonUrl = DataJsonUrl.Trim();
        NeteaseApiBase = NeteaseApiBase.Trim();
        if (NeteaseApiBase.Length == 0) NeteaseApiBase = "https://wyyapi.hjymoon.us.ci/";
        NeteaseCookie = NeteaseCookie.Trim();

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
