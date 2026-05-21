using System.IO;
using System.Text.Json;

namespace FreewriteWindows;

internal static class AgentDebugLog
{
    private const string SessionId = "f06a51";

    private static readonly string[] LogPaths =
    [
        @"c:\Users\thien\OneDrive\Desktop\projects\freewrite_wd\debug-f06a51.log",
        Path.Combine(AppContext.BaseDirectory, "debug-f06a51.log"),
    ];

    // #region agent log
    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["runId"] = runId,
            };

            var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
            foreach (var path in LogPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(path, line);
                }
                catch
                {
                    // Try next path.
                }
            }
        }
        catch
        {
            // Debug logging must never break the app.
        }
    }
    // #endregion
}
