// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ArtifactPostProcessingHelperTests
{
    // ArtifactPostProcessingHelper is linked into each report-engine assembly, so resolve the
    // TrxReport copy through reflection to avoid an ambiguous type reference.
    private static readonly Type HelperType =
        typeof(TrxReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ArtifactPostProcessingHelper")
        ?? throw new InvalidOperationException("Could not find type ArtifactPostProcessingHelper in the Trx report engine assembly.");

    private static readonly MethodInfo OrderInputsMethod =
        HelperType.GetMethod("OrderInputs", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ArtifactPostProcessingHelper.OrderInputs.");

    private static readonly MethodInfo IsReparsePointMethod =
        HelperType.GetMethod("IsReparsePoint", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ArtifactPostProcessingHelper.IsReparsePoint.");

    private readonly List<string> _trackedDirectories = [];

    [TestCleanup]
    public void Cleanup()
    {
        IOException? cleanupException = null;

        for (int i = _trackedDirectories.Count - 1; i >= 0; i--)
        {
            string directory = _trackedDirectories[i];
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException ex)
            {
                cleanupException ??= ex;
            }
        }

        if (cleanupException is not null)
        {
            throw cleanupException;
        }
    }

    [TestMethod]
    public void OrderInputs_ModuleMetadataEnabled_SortsByPathThenModuleMetadataAndExecutionId()
    {
        string firstPath = Path.Combine("artifacts", "B.trx");
        string secondPath = Path.Combine("artifacts", "a.trx");
        InputArtifact byPath = CreateInput(firstPath, "z", "z", "z", "z");
        InputArtifact byModule = CreateInput(secondPath, "B", "z", "z", "z");
        InputArtifact byTargetFramework = CreateInput(secondPath, "a", "B", "z", "z");
        InputArtifact byArchitecture = CreateInput(secondPath, "a", "a", "B", "z");
        InputArtifact byExecutionIdUppercase = CreateInput(secondPath, "a", "a", "a", "A");
        InputArtifact byExecutionIdLowercase = CreateInput(secondPath, "a", "a", "a", "a");

        IReadOnlyList<InputArtifact> result = InvokeOrderInputs(
            [byExecutionIdLowercase, byArchitecture, byTargetFramework, byPath, byExecutionIdUppercase, byModule],
            includeModuleMetadata: true);

        Assert.AreSequenceEqual(
            [byPath, byModule, byTargetFramework, byArchitecture, byExecutionIdUppercase, byExecutionIdLowercase],
            result);
    }

    [TestMethod]
    public void OrderInputs_ModuleMetadataDisabled_SortsByExecutionIdWithoutUsingModuleMetadata()
    {
        string path = Path.Combine("artifacts", "report.trx");
        InputArtifact metadataFirst = CreateInput(path, "a", "a", "a", "2");
        InputArtifact executionIdFirst = CreateInput(path, "z", "z", "z", "1");

        IReadOnlyList<InputArtifact> result =
            InvokeOrderInputs([metadataFirst, executionIdFirst], includeModuleMetadata: false);

        Assert.AreSequenceEqual([executionIdFirst, metadataFirst], result);
    }

    [TestMethod]
    public void OrderInputs_EquivalentRelativeAndFullPaths_UsesExecutionIdAsTieBreaker()
    {
        string relativePath = Path.Combine(".", "artifact.trx");
        string fullPath = Path.GetFullPath(relativePath);
        InputArtifact relativePathInput = CreateInput(relativePath, "module", "tfm", "architecture", "2");
        InputArtifact fullPathInput = CreateInput(fullPath, "module", "tfm", "architecture", "1");

        IReadOnlyList<InputArtifact> result =
            InvokeOrderInputs([relativePathInput, fullPathInput], includeModuleMetadata: false);

        Assert.AreSequenceEqual([fullPathInput, relativePathInput], result);
    }

    [TestMethod]
    public void IsReparsePoint_RegularDirectory_ReturnsFalse()
    {
        string directory = CreateTrackedDirectory();

        bool result = Invoke(directory);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsReparsePoint_NonExistentPath_ReturnsTrue()
    {
        string path = Path.Combine(Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));

        // A non-existent path cannot be inspected, so the method conservatively returns true.
        bool result = Invoke(path);

        Assert.IsTrue(result);
    }

#if NETCOREAPP
    [TestMethod]
    public void IsReparsePoint_SymbolicLinkToDirectory_ReturnsTrue()
    {
        string target = CreateTrackedDirectory();
        string linkPath = Path.Combine(Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        _trackedDirectories.Add(linkPath);

        try
        {
            Directory.CreateSymbolicLink(linkPath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symbolic link creation is not supported or permitted in this environment: {ex.Message}");
            return;
        }

        bool result = Invoke(linkPath);

        Assert.IsTrue(result);
    }
#endif

#if NETFRAMEWORK
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task IsReparsePoint_JunctionToDirectory_ReturnsTrue()
    {
        string target = CreateTrackedDirectory();
        string junctionPath = Path.Combine(Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        _trackedDirectories.Add(junctionPath);

        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{target}\"",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string standardOutput = await standardOutputTask;
        string standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            Assert.Inconclusive(
                $"Junction creation failed with exit code {process.ExitCode}: "
                + $"{standardOutput}{standardError}");
            return;
        }

        bool result = Invoke(junctionPath);

        Assert.IsTrue(result);
    }
#endif

    [TestMethod]
    public void IsReparsePoint_File_ReturnsFalse()
    {
        string directory = CreateTrackedDirectory();
        string filePath = Path.Combine(directory, "file.txt");
        File.WriteAllText(filePath, "content");

        bool result = Invoke(filePath);

        Assert.IsFalse(result);
    }

    private static InputArtifact CreateInput(
        string path,
        string producingTestModule,
        string targetFramework,
        string architecture,
        string executionId)
        => new(
            path,
            kind: null,
            producingTestModule,
            targetFramework,
            architecture,
            executionId);

    private static IReadOnlyList<InputArtifact> InvokeOrderInputs(
        IEnumerable<InputArtifact> inputs,
        bool includeModuleMetadata)
        => ((IEnumerable<InputArtifact>)OrderInputsMethod.Invoke(null, [inputs, includeModuleMetadata])!).ToList();

    private string CreateTrackedDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _trackedDirectories.Add(path);
        return path;
    }

    private static bool Invoke(string path)
        => (bool)IsReparsePointMethod.Invoke(null, [path])!;
}
