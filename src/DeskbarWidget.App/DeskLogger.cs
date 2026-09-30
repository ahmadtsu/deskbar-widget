using System.IO;

namespace DeskbarWidget.App;

/// <summary>
/// Minimal file logger so silent failures become diagnosable.
/// Log: %LocalAppData%\DeskbarWidget\deskbar.log (fresh file per launch).
/// </summary>
internal static class DeskLogger
{
    private static readonly object Gate = new();
    private static readonly string Path;

    static DeskLogger()
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskbarWidget");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, "deskbar.log");
            File.WriteAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] starting{Environment.NewLine}");
        }
        catch
        {
            Path = string.Empty;
        }
    }

    public static void Info(string message)
    {
        if (string.IsNullOrEmpty(Path))
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }

    public static string LogPath => Path;
}
