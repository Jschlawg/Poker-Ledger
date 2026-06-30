using PokerHost.Services;

namespace PokerHost.Tests;

internal sealed class TempAppData : IDisposable
{
    private readonly string? _previousRoot;

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "PokerHost.Tests", Guid.NewGuid().ToString("N"));

    public TempAppData()
    {
        _previousRoot = Environment.GetEnvironmentVariable("POKERHOST_APPDATA_ROOT");
        Environment.SetEnvironmentVariable("POKERHOST_APPDATA_ROOT", Root);
    }

    public AppPaths CreatePaths()
    {
        return new AppPaths();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("POKERHOST_APPDATA_ROOT", _previousRoot);

        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(directory, FileAttributes.Directory);
        }

        Directory.Delete(Root, recursive: true);
    }
}
