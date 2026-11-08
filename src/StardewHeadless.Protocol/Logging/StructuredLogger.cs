using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StardewHeadless.Protocol.Logging;

/// <summary>
/// Logger thread-safe yang menulis ke file .jsonl dan .log dengan FileShare.ReadWrite
/// serta mengelola ring-buffer in-memory untuk observabilitas real-time via API Sysadmin.
/// </summary>
public class StructuredLogger : IDisposable
{
    private readonly string _source;
    private readonly string? _jsonlPath;
    private readonly string? _logPath;
    private readonly bool _echoToConsole;
    private readonly object _lock = new();

    /// <summary>
    /// Event hook eksternal saat terjadi log level Error atau Fatal (misal dikirim ke IJPLabs Telemetry Gateway).
    /// </summary>
    public static Action<LogLevel, string, string, string, Exception?>? OnErrorCaptured;

    private readonly ConcurrentQueue<LogEntry> _memoryBuffer = new();
    private const int MaxBufferSize = 1000;

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
        PreloadHistoricalLogs();
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

    private void PreloadHistoricalLogs()
    {
        if (string.IsNullOrWhiteSpace(_jsonlPath) || !File.Exists(_jsonlPath)) return;

        try
        {
            using var fs = new FileStream(_jsonlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                    if (lines.Count > 300)
                    {
                        lines.RemoveAt(0);
                    }
                }
            }

            foreach (var l in lines)
            {
                var entry = LogEntry.FromJson(l);
                if (entry != null)
                {
                    _memoryBuffer.Enqueue(entry);
                }
            }

            while (_memoryBuffer.Count > MaxBufferSize)
            {
                _memoryBuffer.TryDequeue(out _);
            }
        }
        catch
        {
            // Abaikan error pembacaan log historis saat inisialisasi awal
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

        // Notifikasi ke sistem error tracking (IJPLabs Telemetry Gateway) jika Error / Fatal
        if (level is LogLevel.Error or LogLevel.Fatal)
        {
            try { OnErrorCaptured?.Invoke(level, _source, category, message, exception); } catch { }
        }

        // Simpan ke ring-buffer memori untuk query cepat API Sysadmin
        _memoryBuffer.Enqueue(entry);
        while (_memoryBuffer.Count > MaxBufferSize)
        {
            _memoryBuffer.TryDequeue(out _);
        }

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

    /// <summary>
    /// Mengambil daftar log terbaru dari ring buffer dengan filter fleksibel.
    /// </summary>
    public IReadOnlyList<LogEntry> GetRecentLogs(int limit = 100, string? level = null, string? category = null, string? search = null)
    {
        var list = _memoryBuffer.ToList();
        IEnumerable<LogEntry> query = list;

        if (!string.IsNullOrWhiteSpace(level) && !level.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                (e.Message != null && e.Message.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (e.Category != null && e.Category.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (e.Source != null && e.Source.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        return query.TakeLast(Math.Clamp(limit, 1, 500)).ToList();
    }

    /// <summary>
    /// Mengambil seluruh kategori log yang ada di buffer saat ini.
    /// </summary>
    public IReadOnlyList<string> GetAvailableCategories()
    {
        return _memoryBuffer.Select(e => e.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
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
