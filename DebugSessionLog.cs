using System.IO;
using System.Text.Json;

namespace FreewriteWindows;

internal static class DebugSessionLog
{
    private const string SessionId = "f06a51";

    public static void Write(string location, string message, object data, string hypothesisId, string runId = "min-fix")
    {
        // #region agent log
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = SessionId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                location,
                message,
                data,
                hypothesisId,
                runId,
            }) + Environment.NewLine;
            foreach (var path in ResolveLogPaths())
            {
                try { File.AppendAllText(path, line); } catch { }
            }
        }
        catch { }
        // #endregion
    }

    private static string[] ResolveLogPaths() =>
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "OneDrive", "Desktop", "projects", "freewrite_wd", "debug-f06a51.log"),
        Path.Combine(AppContext.BaseDirectory, "debug-f06a51.log"),
    ];
}
