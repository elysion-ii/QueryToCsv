namespace QueryToCsv.TestSupport;

// Where the repository is, for tests that read a committed file or a build output.
public static class Repository
{
    public static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QueryToCsv.slnx")))
                directory = directory.Parent;

            return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
        }
    }
}
