namespace Awavio.Core;

public sealed record AppPaths(string Home)
{
    public string Config => System.IO.Path.Combine(Home, "config.json");
    public string Journal => System.IO.Path.Combine(Home, "recovery-journal.json");
    public string LogFile => System.IO.Path.Combine(Home, "Logs", "awavio.log");

    /// <summary>AWAVIO_HOME overrides the default %LOCALAPPDATA%\Awavio.</summary>
    public static AppPaths Default()
    {
        var overridden = Environment.GetEnvironmentVariable("AWAVIO_HOME");
        return new AppPaths(string.IsNullOrWhiteSpace(overridden)
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Awavio")
            : overridden);
    }
}
