namespace UnskipClosedTests.Tool;

internal static partial class ApplyEngine
{
    private static string? RestoreUnexpectedSourceMutations(
        GitRepository repository,
        IReadOnlyDictionary<string, byte[]> expectedBytes)
    {
        string? firstMutation = null;
        foreach ((string path, byte[] expected) in expectedBytes)
        {
            string fullPath = PathRules.ResolveInsideRoot(repository.Root, path, "candidate path");
            byte[] actual = ReadBytes(fullPath);
            if (!actual.AsSpan().SequenceEqual(expected))
            {
                firstMutation ??= path;
                WriteBytes(fullPath, expected);
            }
        }

        return firstMutation;
    }

    private static byte[] ReadBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InfrastructureException($"Could not read '{path}'.", ex);
        }
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InfrastructureException($"Could not write '{path}'.", ex);
        }
    }

    private static void RestoreFiles(GitRepository repository, IReadOnlyDictionary<string, byte[]> files)
    {
        foreach ((string path, byte[] bytes) in files)
        {
            WriteBytes(PathRules.ResolveInsideRoot(repository.Root, path, "candidate path"), bytes);
        }
    }

    private static void ResetDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InfrastructureException($"Could not prepare verification directory '{path}'.", ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"warning: could not remove verification directory '{path}': {ex.Message}");
        }
    }
}
