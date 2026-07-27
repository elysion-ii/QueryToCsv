namespace QueryToCsv.Tests;

// A throwaway directory under %TEMP%, removed when the test finishes. Keeps every test
// self-sufficient: no shared fixture directory, no leftovers between runs.
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"QueryToCsvTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* best effort cleanup */ }
        catch (UnauthorizedAccessException) { /* best effort cleanup */ }
    }
}
