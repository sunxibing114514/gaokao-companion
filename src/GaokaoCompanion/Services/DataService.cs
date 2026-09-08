using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GaokaoCompanion.Models;

namespace GaokaoCompanion.Services;

/// <summary>外部 JSON 数据抓取与解析(倒计时数据、句子语音映射)。</summary>
public static class DataService
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    /// <summary>解析 { "date": "...", "event": "...", "sentences": [...] }</summary>
    public static async Task<CountdownData> FetchCountdownAsync(string url, CancellationToken ct = default)
    {
        string json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON 根节点不是对象");

        var data = new CountdownData();
        if (root.TryGetProperty("date", out var dateEl))
        {
            if (dateEl.ValueKind == JsonValueKind.String) data.Date = dateEl.GetString();
            else if (dateEl.ValueKind == JsonValueKind.Number) data.Date = dateEl.GetRawText();
        }

        if (root.TryGetProperty("event", out var eventEl) && eventEl.ValueKind == JsonValueKind.String)
            data.Event = eventEl.GetString();

        if (root.TryGetProperty("sentences", out var sen))
        {
            if (sen.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in sen.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String &&
                        item.GetString() is { } s && s.Trim().Length > 0)
                    {
                        data.Sentences.Add(s.Trim());
                    }
                }
            }
            else if (sen.ValueKind == JsonValueKind.String &&
                     sen.GetString() is { } single && single.Trim().Length > 0)
            {
                data.Sentences.Add(single.Trim());
            }
        }

        return data;
    }

    /// <summary>解析 { "句子": "声音链接", ... }</summary>
    public static async Task<Dictionary<string, string>> FetchSoundsAsync(string url, CancellationToken ct = default)
    {
        string json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new Dictionary<string, string>();
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON 根节点不是对象,期望格式 {\"句子\":\"声音链接\"}");

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String &&
                prop.Value.GetString() is { } link && link.Trim().Length > 0)
            {
                result[prop.Name.Trim()] = link.Trim();
            }
        }

        return result;
    }
}
