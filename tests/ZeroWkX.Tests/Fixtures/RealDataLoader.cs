namespace ZeroWkX.Tests.Fixtures;

public static class RealDataLoader
{
    public static byte[] ReadWkb(string fileName) => File.ReadAllBytes(Locate(fileName));

    private static string Locate(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 20 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "ZeroWkX.slnx")) || File.Exists(Path.Combine(dir, "ZeroWkX.sln")))
            {
                var candidate = Path.Combine(dir, "bench", "Data", fileName);
                if (File.Exists(candidate)) return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(
            $"Could not locate {fileName}. Expected under <repo>/bench/Data/. See bench/Data/README.md.");
    }
}
