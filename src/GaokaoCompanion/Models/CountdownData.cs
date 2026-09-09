using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GaokaoCompanion.Models;

/// <summary>外部倒计时 JSON:{ "date": "2026-06-07 09:00", "event": "2026年高考", "sentences": ["…"], "color": "#000000" }</summary>
public class CountdownData
{
    public string? Date { get; set; }
    public string? Event { get; set; }
    public List<string> Sentences { get; set; } = new();

    /// <summary>组件文字颜色(可选,如 "#000000"/"#FF5500"/"#80000000");空 = 使用默认黑色。</summary>
    public string? Color { get; set; }

    public string EventDisplay => string.IsNullOrWhiteSpace(Event) ? "高考" : Event!;

    private static readonly Regex LooseDateRegex = new(
        @"(?<y>\d{4})\D{1,2}(?<m>\d{1,2})\D{1,2}(?<d>\d{1,2})(?:\D{1,2}(?<h>\d{1,2}):(?<mi>\d{1,2})(?::(?<s>\d{1,2}))?)?",
        RegexOptions.Compiled);

    public DateTime? TargetDate
    {
        get
        {
            string raw = Date?.Trim() ?? "";
            if (raw.Length == 0) return null;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;

            var m = LooseDateRegex.Match(raw);
            if (m.Success)
            {
                try
                {
                    return new DateTime(
                        int.Parse(m.Groups["y"].Value),
                        int.Parse(m.Groups["m"].Value),
                        int.Parse(m.Groups["d"].Value),
                        m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0,
                        m.Groups["mi"].Success ? int.Parse(m.Groups["mi"].Value) : 0,
                        m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value) : 0);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}
