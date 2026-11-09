using System.Diagnostics;
using System.IO;
using System.Text;

namespace ShadeFlow.Services;

/// <summary>Small best-effort local log. Logging is deliberately never allowed to affect dimming.</summary>
public sealed class LogService : IDisposable
{
    private const int RetainedFiles = 7;
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly Func<DateTime> _now;
    private bool _disposed;

    public static LogService? Current { get; set; }

    public LogService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShadeFlow", "Logs"),
        () => DateTime.Now)
    {
    }

    internal LogService(string directory, Func<DateTime> now)
    {
        _directory = directory;
        _now = now;
        try
        {
            Directory.CreateDirectory(_directory);
            RemoveExpiredFiles();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("ShadeFlow log initialization failed: {0}", ex);
        }
    }

    public string DirectoryPath => _directory;

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (_disposed) return;
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                var now = _now();
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, $"shadeflow-{now:yyyy-MM-dd}.log");
                var line = new StringBuilder()
                    .Append(now.ToString("O"))
                    .Append(' ').Append(level).Append(' ').Append(message);
                if (exception is not null)
                    line.AppendLine().Append(exception);
                line.AppendLine();
                File.AppendAllText(path, line.ToString());
                RemoveExpiredFiles();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Trace is the last resort; never recurse through this logger.
            Trace.TraceWarning("ShadeFlow log write failed: {0}", ex);
        }
    }

    private void RemoveExpiredFiles()
    {
        var files = Directory.EnumerateFiles(_directory, "shadeflow-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(RetainedFiles)
            .ToArray();
        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Old ShadeFlow log cleanup failed: {0}", ex);
            }
        }
    }

    public void Dispose() => _disposed = true;
}
