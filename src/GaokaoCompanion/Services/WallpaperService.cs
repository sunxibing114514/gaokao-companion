using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using GaokaoCompanion.Models;

namespace GaokaoCompanion.Services;

/// <summary>
/// 定时壁纸切换:当时间为「星期 x 点 x 分」时切换为指定壁纸。
/// 规则保存在 config.json,可在设置 GUI 中编辑。
/// </summary>
public class WallpaperService
{
    private readonly AppConfig _config;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private string _lastKey = "";

    public WallpaperService(AppConfig config)
    {
        _config = config;
        _timer.Tick += (s, e) => Check();
    }

    public void Start()
    {
        Check();
        _timer.Start();
    }

    public void Restart()
    {
        _timer.Stop();
        Check();
        _timer.Start();
    }

    public void Check()
    {
        try
        {
            var now = DateTime.Now;
            foreach (var rule in _config.Wallpapers)
            {
                if (string.IsNullOrWhiteSpace(rule.ImagePath)) continue;

                if (rule.Day != 0)
                {
                    // Day:0=每天,1=周一 … 7=周日;.NET DayOfWeek:Sunday=0 … Saturday=6
                    int dotNetDay = rule.Day == 7 ? 0 : rule.Day;
                    if (dotNetDay != (int)now.DayOfWeek) continue;
                }

                if (rule.Hour != now.Hour || rule.Minute != now.Minute) continue;

                string key = $"{rule.Day}|{rule.Hour}|{rule.Minute}|{rule.ImagePath}|{now:yyyyMMddHHmm}";
                if (key == _lastKey) continue;

                if (SetWallpaper(rule.ImagePath))
                {
                    _lastKey = key;
                    Logger.Info("已切换壁纸:" + rule.ImagePath);
                }
                else
                {
                    Logger.Error("切换壁纸失败(文件不存在或调用失败):" + rule.ImagePath);
                }

                break; // 第一条命中的规则生效
            }
        }
        catch (Exception ex)
        {
            Logger.Error("壁纸检查异常", ex);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    private const uint SpiSetDesktopWallpaper = 20;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendChange = 0x02;

    public static bool SetWallpaper(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            string full = Path.GetFullPath(path);
            return SystemParametersInfo(SpiSetDesktopWallpaper, 0, full, SpifUpdateIniFile | SpifSendChange);
        }
        catch (Exception ex)
        {
            Logger.Error("SystemParametersInfo 异常", ex);
            return false;
        }
    }
}
