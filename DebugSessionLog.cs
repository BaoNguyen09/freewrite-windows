using System.IO;
using System.Text.Json;

namespace FreewriteWindows;

internal static class DebugSessionLog
{
    private const string SessionId = "f06a51";
    private static readonly string LogPath = ResolveLogPath();

    public static void Write(
        string location,
        string message,
        object data,
        string hypothesisId,
        string runId = "pre-fix")
    {
        // #region agent log
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = SessionId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                location,
                message,
                data,
                hypothesisId,
                runId,
                logPath = LogPath,
            });
            File.AppendAllText(LogPath, payload + Environment.NewLine);
        }
        catch
        {
            // ignore logging failures
        }
        // #endregion
    }

    private static string ResolveLogPath()
    {
        var workspaceLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "OneDrive",
            "Desktop",
            "projects",
            "freewrite_wd",
            "debug-f06a51.log");
        if (Directory.Exists(Path.GetDirectoryName(workspaceLog)))
        {
            return workspaceLog;
        }

        return Path.Combine(AppContext.BaseDirectory, "debug-f06a51.log");
    }
}
