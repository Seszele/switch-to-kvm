namespace SwitchToKVM.Core;

public static class Log
{
    private const long MaxSize = 1_000_000;
    private static readonly Lock Lock = new();

    public static void Write(string message)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Folder);
                if (new FileInfo(AppPaths.Log) is { Exists: true, Length: > MaxSize })
                    File.Move(AppPaths.Log, AppPaths.Log + ".old", overwrite: true);
                File.AppendAllText(AppPaths.Log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
        }
    }
}
