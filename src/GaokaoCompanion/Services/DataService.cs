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

    /// <summary>
    /// 提取文本中所有顶层 JSON 值。兼容:两个 JSON 对象拼在同一个文件、UTF-8 BOM、
    /// 前后夹杂的非 JSON 内容(如错误页 HTML)。逐段返回完整 JSON 值字符串。
    /// </summary>
    private static List<string> ExtractTopLevelValues(string text)
    {
        var values = new List<string>();
        string json = text.TrimStart('\uFEFF');
        int i = 0, n = json.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(json[i])) i++;
            if (i >= n) break;
            if (json[i] != '{' && json[i] != '[')
            {
                while (i < n && json[i] != '{' && json[i] != '[') i++; // 跳过垃圾前缀
                continue;
            }
            int start = i, depth = 0, j = i;
            bool inString = false, escape = false;
            for (; j < n; j++)
            {
                char c = json[j];
                if (inString)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0) { j++; break; }
                }
            }
            if (depth != 0) break; // JSON 未闭合,交给解析器给出明确错误
            values.Add(json[start..j]);
            i = j;
        }
        return values;
    }

    /// <summary>解析 { "date": "...", "event": "...", "sentences": [...] }
    /// 支持一个文件里连续放了多个 JSON(自动挑出倒计时那个),两个 URL 甚至可指向同一文件。</summary>
    public static async Task<CountdownData> FetchCountdownAsync(string url, CancellationToken ct = default)
    {
        string json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        foreach (string value in ExtractTopLevelValues(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                var data = ParseCountdown(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(data.Date) || data.Sentences.Count > 0)
                    return data;
                // 合法 JSON 但没有 date/sentences(比如同文件里的 sounds 对象)→ 尝试下一个值
            }
            catch (JsonException) { /* 该段不是合法 JSON → 尝试下一个值 */ }
        }
        throw new InvalidOperationException("JSON 中没有找到倒计时数据(需要 date / event / sentences 字段)");
    }

    private static CountdownData ParseCountdown(JsonElement root)
    {
        var data = new CountdownData();
        if (root.ValueKind != JsonValueKind.Object)
            return data;

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

    /// <summary>解析 { "句子": "声音链接", ... },同样支持一个文件里多个 JSON(自动跳过倒计时对象)。</summary>
    public static async Task<Dictionary<string, string>> FetchSoundsAsync(string url, CancellationToken ct = default)
    {
        string json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
        foreach (string value in ExtractTopLevelValues(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(value);
                var result = ParseSounds(doc.RootElement);
                if (result.Count > 0) return result;
            }
            catch (JsonException) { }
        }
        throw new InvalidOperationException("JSON 中没有找到句子→声音映射(期望格式 {\"句子\":\"链接\"})");
    }

    private static Dictionary<string, string> ParseSounds(JsonElement root)
    {
        var result = new Dictionary<string, string>();
        if (root.ValueKind != JsonValueKind.Object)
            return result;

        // 含倒计时专属字段的对象不是 sounds 映射(两个 JSON 同文件时避免误认)
        if (root.TryGetProperty("date", out _) || root.TryGetProperty("sentences", out _))
            return result;

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
