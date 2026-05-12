namespace IoTEdge.BasicRuntime.Tests;

internal static class TestSupport
{
    public static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "IoTEdge.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate the repository root.");
    }

    public static string SamplePath(string fileName)
        => Path.GetFullPath(Path.Combine(RepoRoot(), "samples", "my_basic", fileName));

    public static string YardPath(string fileName)
        => Path.GetFullPath(Path.Combine(RepoRoot(), "samples", "my_basic", "yard", fileName));

    public static string Normalize(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string OutputText(BasicRuntimeResult result)
        => Normalize(string.Concat(result.Output));
}
