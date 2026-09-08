namespace GaokaoCompanion.Services;

/// <summary>极简文件日志,写在 exe 同目录 log.txt,便于排查问题。</summary>
public static class Logger
{
    private static readonly object Lock = new();

    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "log.txt");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", message + (ex == null ? "" : " => " + ex));

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
