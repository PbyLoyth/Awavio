namespace Awavio.Core;

/// <summary>Append-only text log. Logging never throws.</summary>
public sealed class Log(string path)
{
    readonly object gate = new();

    public string Path { get; } = path;

    public void Write(string message)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
                File.AppendAllText(Path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
