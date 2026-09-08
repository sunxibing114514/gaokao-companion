using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GaokaoCompanion.Services;

public class NeteaseSong
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    /// <summary>true = 来自歌词搜索结果(播放时跳到歌词对应位置)</summary>
    public bool FromLyric { get; set; }
}

/// <summary>
/// NeteaseCloudMusicApi / NeteaseCloudMusicApiEnhanced 兼容客户端。
/// API 地址保存在 config.json 的 neteaseApiBase,不硬编码。
/// </summary>
public class NeteaseClient
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public string Base { get; set; } = "";

    /// <summary>
    /// 网易云 Cookie(如 MUSIC_U=xxx;…),保存在 config.json 的 neteaseCookie。
    /// 提供后随每个 API 请求发送,可解锁 VIP/更高音质;留空则匿名访问。
    /// </summary>
    public string Cookie { get; set; } = "";

    /// <summary>带可选 Cookie 头的 GET(每个请求独立 header,避免共享 DefaultRequestHeaders 的线程问题)。</summary>
    private async Task<string> GetWithCookieAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        string cookie = Cookie.Trim();
        if (cookie.Length > 0)
        {
            // 允许用户只填 MUSIC_U 的值,自动补全键名
            if (!cookie.Contains('=', StringComparison.Ordinal)) cookie = "MUSIC_U=" + cookie;
            req.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private string Api(string pathAndQuery)
    {
        string b = Base.Trim().TrimEnd('/');
        return b + pathAndQuery;
    }

    /// <summary>同时搜索歌名(type=1)与歌词(type=1006),合并去重。</summary>
    public async Task<List<NeteaseSong>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        keyword = keyword.Trim();
        if (keyword.Length == 0) return new List<NeteaseSong>();
        if (Base.Trim().Length == 0) throw new InvalidOperationException("未配置网易云 API 地址");

        var songTask = SearchTypeSafeAsync(keyword, 1, false, ct);
        var lyricTask = SearchTypeSafeAsync(keyword, 1006, true, ct);
        await Task.WhenAll(songTask, lyricTask).ConfigureAwait(false);

        var (songs, songErr) = songTask.Result;
        var (lyricSongs, lyricErr) = lyricTask.Result;

        if (songs.Count == 0 && lyricSongs.Count == 0)
        {
            if (songErr != null || lyricErr != null) throw songErr ?? lyricErr!;
            return new List<NeteaseSong>();
        }

        var merged = new List<NeteaseSong>(songs.Count + lyricSongs.Count);
        var seen = new HashSet<long>();
        foreach (var s in songs)
        {
            if (seen.Add(s.Id)) merged.Add(s);
        }
        foreach (var s in lyricSongs)
        {
            if (seen.Add(s.Id)) merged.Add(s);
        }
        return merged;
    }

    private async Task<(List<NeteaseSong> Songs, Exception? Error)> SearchTypeSafeAsync(
        string keyword, int type, bool fromLyric, CancellationToken ct)
    {
        try
        {
            string url = Api("/search?keywords=" + Uri.EscapeDataString(keyword) +
                             "&type=" + type + "&limit=30");
            string json = await GetWithCookieAsync(url, ct).ConfigureAwait(false);
            return (ParseSearchResult(json, fromLyric), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (new List<NeteaseSong>(), ex);
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static string JoinNames(JsonElement arr)
    {
        var names = new List<string>();
        foreach (var a in arr.EnumerateArray())
        {
            var n = GetString(a, "name");
            if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
        }
        return string.Join(" / ", names);
    }

    private static List<NeteaseSong> ParseSearchResult(string json, bool fromLyric)
    {
        var list = new List<NeteaseSong>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return list;
        if (!root.TryGetProperty("result", out var result)) return list;
        if (!result.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array) return list;

        foreach (var s in songs.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            long id = 0;
            if (s.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                id = idEl.GetInt64();
            if (id == 0) continue;

            string name = GetString(s, "name") ?? "未知歌曲";
            string artist = "";
            if (s.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
                artist = JoinNames(artists);
            else if (s.TryGetProperty("ar", out var ar) && ar.ValueKind == JsonValueKind.Array)
                artist = JoinNames(ar);

            list.Add(new NeteaseSong { Id = id, Name = name, Artist = artist, FromLyric = fromLyric });
        }
        return list;
    }

    /// <summary>获取播放链接(/song/url/v1,失败时回退 /song/url);null 表示无版权或需要 VIP。</summary>
    public async Task<string?> GetSongUrlAsync(long id, CancellationToken ct = default)
    {
        if (Base.Trim().Length == 0) throw new InvalidOperationException("未配置网易云 API 地址");

        Exception? lastError = null;
        string[] paths =
        {
            "/song/url/v1?id=" + id + "&level=standard",
            "/song/url?id=" + id,
        };

        foreach (var path in paths)
        {
            try
            {
                string json = await GetWithCookieAsync(Api(path), ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? url = null;
                if (root.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                {
                    var first = data[0];
                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("url", out var u1) &&
                        u1.ValueKind == JsonValueKind.String)
                    {
                        url = u1.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(url) &&
                    root.TryGetProperty("url", out var u2) &&
                    u2.ValueKind == JsonValueKind.String)
                {
                    url = u2.GetString();
                }

                if (!string.IsNullOrWhiteSpace(url)) return url;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError != null) throw lastError;
        return null;
    }

    private static readonly Regex LyricLineRegex = new(
        @"^\[(?<min>\d{1,3}):(?<sec>\d{1,2})(?:[.:](?<frac>\d{1,3}))?\]\s*(?<text>.*)$",
        RegexOptions.Compiled);

    private static string NormalizeForMatch(string s)
        => s.Replace(" ", "").Replace("　", "").ToLowerInvariant();

    /// <summary>在歌词(LRC)中定位包含关键词的第一句,返回其时间点;找不到返回 null。</summary>
    public async Task<TimeSpan?> FindLyricTimeAsync(long id, string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return null;

        string json = await GetWithCookieAsync(Api("/lyric?id=" + id), ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("lrc", out var lrc) ||
            !lrc.TryGetProperty("lyric", out var lyricEl) ||
            lyricEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string lyric = lyricEl.GetString() ?? "";
        string normKeyword = NormalizeForMatch(keyword);

        foreach (var raw in lyric.Split('\n'))
        {
            var m = LyricLineRegex.Match(raw.TrimEnd('\r'));
            if (!m.Success) continue;
            string text = m.Groups["text"].Value.Trim();
            if (text.Length == 0) continue;
            if (!text.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !NormalizeForMatch(text).Contains(normKeyword))
            {
                continue;
            }

            int min = int.Parse(m.Groups["min"].Value);
            int sec = int.Parse(m.Groups["sec"].Value);
            double seconds = min * 60 + sec;
            var frac = m.Groups["frac"];
            if (frac.Success)
            {
                string f = frac.Value;
                seconds += int.Parse(f) / Math.Pow(10, f.Length);
            }

            if (seconds > 0.5) seconds -= 0.3; // 稍微提前一点,听感更自然
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }
}
