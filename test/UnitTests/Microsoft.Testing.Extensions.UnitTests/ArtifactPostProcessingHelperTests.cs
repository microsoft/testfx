// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class ArtifactPostProcessingHelperTests
{
    // ArtifactPostProcessingHelper is an internal type that is *linked* (compiled) into every
    // artifact-post-processing engine assembly (Trx, Html, JUnit, Ctrf), so referencing it by name
    // directly is ambiguous (CS0433). Resolve the method via reflection, anchoring on one public
    // type from the Trx engine assembly, mirroring ReportFileNameSanitizationConsistencyTests.
    private static readonly MethodInfo IsReparsePointMethod =
        (typeof(TrxReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ArtifactPostProcessingHelper")
            ?? throw new InvalidOperationException("Could not find type ArtifactPostProcessingHelper in the Trx report engine assembly."))
        .GetMethod("IsReparsePoint", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ArtifactPostProcessingHelper.IsReparsePoint.");

    private readonly List<string> _trackedDirectories = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (string directory in _trackedDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
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
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));

        // A path that does not exist cannot have its attributes read, so it is conservatively
        // treated as unsafe (the helper must fail closed, never assume it's a plain directory).
        bool result = Invoke(path);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsReparsePoint_SymbolicLinkToDirectory_ReturnsTrue()
    {
        string target = CreateTrackedDirectory();
        string linkPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        _trackedDirectories.Add(linkPath);

        try
        {
            Directory.CreateSymbolicLink(linkPath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symbolic link creation is not supported/permitted in this environment: {ex.Message}");
            return;
        }

        bool result = Invoke(linkPath);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsReparsePoint_FileInsteadOfDirectory_ReturnsFalse()
    {
        // A plain file is not a reparse point, so it must not be treated as unsafe merely because
        // it is not a directory; callers are expected to have already validated it's a directory.
        string directory = CreateTrackedDirectory();
        string filePath = System.IO.Path.Combine(directory, "file.txt");
        File.WriteAllText(filePath, "content");

        bool result = Invoke(filePath);

        Assert.IsFalse(result);
    }

    private string CreateTrackedDirectory()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(ArtifactPostProcessingHelperTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _trackedDirectories.Add(path);
        return path;
    }

    private static bool Invoke(string path)
        => (bool)IsReparsePointMethod.Invoke(null, [path])!;
}
