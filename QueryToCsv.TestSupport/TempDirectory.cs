namespace QueryToCsv.TestSupport;

// A throwaway directory under %TEMP%, removed when the test finishes. Keeps every test
// self-sufficient: no shared fixture directory, no leftovers between runs.
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"QueryToCsvTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    // Cleanup is best effort: a directory a test could not remove must not fail that test,
    // but it is reported so leftovers under %TEMP% do not pile up unnoticed.
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not remove the test directory {Path}: {ex.Message}");
        }
    }
}
