using System.IO;
using System.Text.Json;

namespace FreewriteWindows;

internal static class DebugSessionLog
{
    private const string SessionId = "f06a51";
    private static readonly string[] LogPaths = ResolveLogPaths();

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
                logPaths = LogPaths,
            });
            var line = payload + Environment.NewLine;
            foreach (var path in LogPaths)
            {
                try
                {
                    File.AppendAllText(path, line);
                }
                catch
                {
                    // try next path
                }
            }
        }
        catch
        {
            // ignore logging failures
        }
        // #endregion
    }

    private static string[] ResolveLogPaths()
    {
        var workspaceLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "OneDrive",
            "Desktop",
            "projects",
            "freewrite_wd",
            "debug-f06a51.log");
        var distLog = Path.Combine(AppContext.BaseDirectory, "debug-f06a51.log");
        return [workspaceLog, distLog];
    }
}
