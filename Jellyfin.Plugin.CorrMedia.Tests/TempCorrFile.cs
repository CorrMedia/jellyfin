namespace Jellyfin.Plugin.CorrMedia.Tests;

internal sealed class TempCorrFile : IDisposable
{
    public TempCorrFile(string json)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "corrmedia-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        FilePath = System.IO.Path.Combine(directory, "clip.corr.json");
        File.WriteAllText(FilePath, json);
    }

    public string FilePath { get; }

    public void Dispose()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
