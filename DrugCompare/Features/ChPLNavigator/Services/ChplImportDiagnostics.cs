using System.IO;

namespace DrugCompare.Features.ChPLNavigator.Services;

internal static class ChplImportDiagnostics
{
    private static readonly object Sync = new();

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (Sync)
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "chpl-import.log"),
                    line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never affect the import flow.
        }
    }
}
