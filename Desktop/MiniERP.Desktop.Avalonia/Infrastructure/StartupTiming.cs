using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Lightweight, best-effort startup profiling. Measurements remain in memory
/// until the main window opens; writing the log never blocks the UI thread.
/// </summary>
internal static class StartupTiming
{
    private static readonly long StartedTimestamp = Stopwatch.GetTimestamp();
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.Now;
    private static readonly object Sync = new();
    private static readonly List<string> Entries = new();
    private static bool _completed;

    public static void Mark(string label)
    {
        var now = Stopwatch.GetTimestamp();
        lock (Sync)
        {
            if (_completed) return;
            Entries.Add($"[+{Stopwatch.GetElapsedTime(StartedTimestamp, now).TotalMilliseconds,8:F1} ms] {label}");
        }
    }

    /// <summary>Reports one stage of an externally measured operation.</summary>
    public static void RecordDuration(string label, TimeSpan duration)
    {
        var now = Stopwatch.GetTimestamp();
        lock (Sync)
        {
            if (_completed) return;
            Entries.Add(
                $"[+{Stopwatch.GetElapsedTime(StartedTimestamp, now).TotalMilliseconds,8:F1} ms] " +
                $"{label}: {duration.TotalMilliseconds:F1} ms");
        }
    }

    public static void Measure(string label, Action action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            action();
        }
        finally
        {
            RecordDuration(label, Stopwatch.GetElapsedTime(started));
        }
    }

    public static T Measure<T>(string label, Func<T> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return action();
        }
        finally
        {
            RecordDuration(label, Stopwatch.GetElapsedTime(started));
        }
    }

    /// <summary>
    /// Persists the current run once, after opening the window (or on failure).
    /// Logging failures must never interfere with the ERP startup.
    /// </summary>
    public static void Complete(string outcome)
    {
        string report;
        lock (Sync)
        {
            if (_completed) return;

            var total = Stopwatch.GetElapsedTime(StartedTimestamp);
            Entries.Add($"[+{total.TotalMilliseconds,8:F1} ms] Finished: {outcome}");
            var builder = new StringBuilder();
            builder.AppendLine(
                $"=== MiniERP startup {StartedAt:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                $"PID {Environment.ProcessId} ===");
            builder.AppendLine(
                $"Runtime: {RuntimeInformation.FrameworkDescription}; " +
                $"OS: {RuntimeInformation.OSDescription}; " +
                $"Architecture: {RuntimeInformation.ProcessArchitecture}");
            foreach (var entry in Entries)
                builder.AppendLine(entry);
            builder.AppendLine();
            report = builder.ToString();
            _completed = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var path = Path.Combine(AppDataPaths.GetDataDirectory(), "startup-timing.log");
                File.AppendAllText(path, report, Encoding.UTF8);
            }
            catch
            {
                // Startup profiling must never prevent the application from running.
            }
        });
    }
}
