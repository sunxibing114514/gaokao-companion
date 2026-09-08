using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GaokaoCompanion.Models;

/// <summary>壁纸切换规则。Day:0=每天,1=周一 … 7=周日。</summary>
public class WallpaperRule : INotifyPropertyChanged
{
    private int _day;
    private int _hour;
    private int _minute;
    private string _imagePath = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public int Day
    {
        get => _day;
        set { if (_day != value) { _day = value; Raise(); } }
    }

    public int Hour
    {
        get => _hour;
        set { if (_hour != value) { _hour = value; Raise(); } }
    }

    public int Minute
    {
        get => _minute;
        set { if (_minute != value) { _minute = value; Raise(); } }
    }

    public string ImagePath
    {
        get => _imagePath;
        set { if (_imagePath != value) { _imagePath = value; Raise(); } }
    }
}
