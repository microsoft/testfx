// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

#if NETFRAMEWORK
using System.Diagnostics;
#endif

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class ArtifactPostProcessingHelperTests
{
    // ArtifactPostProcessingHelper is linked into each report-engine assembly, so resolve the
    // TrxReport copy through reflection to avoid an ambiguous type reference.
    private static readonly Type ArtifactPostProcessingHelperType =
        typeof(TrxReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ArtifactPostProcessingHelper")
            ?? throw new InvalidOperationException("Could not find type ArtifactPostProcessingHelper in the Trx report engine assembly.");

    private static readonly MethodInfo IsReparsePointMethod =
        ArtifactPostProcessingHelperType
        .GetMethod("IsReparsePoint", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ArtifactPostProcessingHelper.IsReparsePoint.");

    private static readonly MethodInfo OrderInputsMethod =
        ArtifactPostProcessingHelperType
        .GetMethod("OrderInputs", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ArtifactPostProcessingHelper.OrderInputs.");

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

    [TestMethod]
    public void OrderInputs_WithoutModuleMetadata_OrdersByPathThenExecutionId()
    {
        // Paths intentionally out of order, and not already sorted alphabetically as full paths either.
        InputArtifact zebra = CreateInput("zebra.trx", executionId: "execution-2");
        InputArtifact apple1 = CreateInput("apple.trx", executionId: "execution-2");
        InputArtifact apple2 = CreateInput("apple.trx", executionId: "execution-1");

        IOrderedEnumerable<InputArtifact> result = InvokeOrderInputs([zebra, apple1, apple2], includeModuleMetadata: false);

        Assert.AreSequenceEqual([apple2, apple1, zebra], result);
    }

    [TestMethod]
    public void OrderInputs_WithModuleMetadata_OrdersByPathThenModuleThenFrameworkThenArchitectureThenExecutionId()
    {
        // Same path for all inputs so only the module-metadata tiebreakers determine order.
        InputArtifact netFxX86 = CreateInput("input.trx", module: "module.dll", targetFramework: "net472", architecture: "x86", executionId: "execution-1");
        InputArtifact netFxX64 = CreateInput("input.trx", module: "module.dll", targetFramework: "net472", architecture: "x64", executionId: "execution-1");
        InputArtifact net8 = CreateInput("input.trx", module: "module.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1");
        InputArtifact otherModule = CreateInput("input.trx", module: "zmodule.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1");

        IOrderedEnumerable<InputArtifact> result = InvokeOrderInputs(
            [otherModule, net8, netFxX64, netFxX86],
            includeModuleMetadata: true);

        Assert.AreSequenceEqual([netFxX64, netFxX86, net8, otherModule], result);
    }

    [TestMethod]
    public void OrderInputs_ComparesPathsByFullPath_NotByRawInputString()
    {
        // A relative path and its equivalent absolute path should sort next to each other, and
        // relative paths must be resolved against the current directory before comparison.
        string relative = "relative-input.trx";
        string absoluteEquivalent = Path.GetFullPath(relative);
        InputArtifact relativeInput = CreateInput(relative, executionId: "execution-2");
        InputArtifact absoluteInput = CreateInput(absoluteEquivalent, executionId: "execution-1");

        IOrderedEnumerable<InputArtifact> result = InvokeOrderInputs([relativeInput, absoluteInput], includeModuleMetadata: false);

        Assert.AreSequenceEqual([absoluteInput, relativeInput], result);
    }

    private string CreateTrackedDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _trackedDirectories.Add(path);
        return path;
    }

    private static bool Invoke(string path)
        => (bool)IsReparsePointMethod.Invoke(null, [path])!;

    private static IOrderedEnumerable<InputArtifact> InvokeOrderInputs(IEnumerable<InputArtifact> inputs, bool includeModuleMetadata)
        => (IOrderedEnumerable<InputArtifact>)OrderInputsMethod.Invoke(null, [inputs, includeModuleMetadata])!;

    private static InputArtifact CreateInput(
        string path,
        string? module = null,
        string? targetFramework = null,
        string? architecture = null,
        string? executionId = null)
        => new(path, kind: null, producingTestModule: module, targetFramework: targetFramework, architecture: architecture, executionId: executionId);
}
