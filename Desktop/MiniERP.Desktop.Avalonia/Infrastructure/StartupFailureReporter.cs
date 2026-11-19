using System.Text;

namespace MiniERP.Desktop.Infrastructure;

internal static class StartupFailureReporter
{
    /// <summary>Best-effort local diagnostic log; never writes the database itself.</summary>
    public static string Write(Exception error)
    {
        var logPath = Path.Combine(AppDataPaths.GetDataDirectory(), "startup-error.log");
        try
        {
            File.AppendAllText(logPath,
                $"[{DateTimeOffset.Now:O}] MiniERP startup failure" + Environment.NewLine +
                error + Environment.NewLine + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Avoid replacing the original startup exception if the log directory
            // is read-only or unavailable.
        }

        return logPath;
    }
}
