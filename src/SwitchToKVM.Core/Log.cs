namespace SwitchToKVM.Core;

public static class Log
{
    private const long MaxSize = 1_000_000;
    private static readonly Lock Lock = new();

    public static string Path { get; set; } = AppPaths.Log;

    public static void Write(string message)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                if (new FileInfo(Path) is { Exists: true, Length: > MaxSize })
                    File.Move(Path, Path + ".old", overwrite: true);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
        }
    }
}
