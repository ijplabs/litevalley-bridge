using System.Text;

namespace StardewHeadless.Protocol.Logging;

/// <summary>
/// Logger thread-safe yang menulis ke file .jsonl dan .log dengan FileShare.ReadWrite
/// agar agent AI dapat membaca log secara real-time tanpa mengganggu proses game/server.
/// </summary>
public class StructuredLogger : IDisposable
{
    private readonly string _source;
    private readonly string? _jsonlPath;
    private readonly string? _logPath;
    private readonly bool _echoToConsole;
    private readonly object _lock = new();

    private StreamWriter? _jsonlWriter;
    private StreamWriter? _logWriter;
    private bool _isDisposed;

    public StructuredLogger(string source, string? jsonlPath = null, string? logPath = null, bool echoToConsole = true)
    {
        _source = source;
        _jsonlPath = jsonlPath;
        _logPath = logPath;
        _echoToConsole = echoToConsole;

        InitializeWriters();
    }

    private void InitializeWriters()
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(_jsonlPath))
            {
                var dir = Path.GetDirectoryName(_jsonlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var fs = new FileStream(_jsonlPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _jsonlWriter = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
            }

            if (!string.IsNullOrWhiteSpace(_logPath))
            {
                var dir = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _logWriter = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
            }
        }
    }

    public void Log(LogLevel level, string category, string message, Dictionary<string, object?>? details = null, Exception? exception = null)
    {
        if (_isDisposed) return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Level = level.ToString().ToUpperInvariant(),
            Source = _source,
            Category = category,
            Message = message,
            Details = details,
            Exception = exception?.ToString()
        };

        var json = entry.ToJson();
        var readable = $"[{entry.Timestamp}] [{entry.Level,-5}] [{entry.Source}::{entry.Category}] {entry.Message}";
        if (exception != null)
        {
            readable += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            try
            {
                _jsonlWriter?.WriteLine(json);
                _logWriter?.WriteLine(readable);
            }
            catch
            {
                // File writing fallback if stream encountered temporary issue
            }

            if (_echoToConsole)
            {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = level switch
                {
                    LogLevel.Trace => ConsoleColor.DarkGray,
                    LogLevel.Debug => ConsoleColor.Gray,
                    LogLevel.Info => ConsoleColor.Cyan,
                    LogLevel.Warn => ConsoleColor.Yellow,
                    LogLevel.Error => ConsoleColor.Red,
                    LogLevel.Fatal => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };
                Console.WriteLine(readable);
                Console.ForegroundColor = prevColor;
            }
        }
    }

    public void Info(string category, string message, Dictionary<string, object?>? details = null)
        => Log(LogLevel.Info, category, message, details);

    public void Debug(string category, string message, Dictionary<string, object?>? details = null)
        => Log(LogLevel.Debug, category, message, details);

    public void Warn(string category, string message, Dictionary<string, object?>? details = null)
        => Log(LogLevel.Warn, category, message, details);

    public void Error(string category, string message, Dictionary<string, object?>? details = null, Exception? ex = null)
        => Log(LogLevel.Error, category, message, details, ex);

    public void Fatal(string category, string message, Dictionary<string, object?>? details = null, Exception? ex = null)
        => Log(LogLevel.Fatal, category, message, details, ex);

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _jsonlWriter?.Flush();
                _jsonlWriter?.Dispose();
                _logWriter?.Flush();
                _logWriter?.Dispose();
            }
            catch
            {
                // Cleanup ignore
            }
        }
    }
}
