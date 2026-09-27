namespace Kantela.Core;

public sealed class AppPaths(string rootDirectory)
{
    public string Root { get; } = rootDirectory;

    public string Database => Path.Combine(Root, "kantela.db");

    public string Backups => Path.Combine(Root, "backups");

    public string Logs => Path.Combine(Root, "logs");

    public string WebView => Path.Combine(Root, "WebView2");

    public static AppPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kantela"));

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Logs);
    }
}
