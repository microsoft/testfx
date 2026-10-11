// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Verifies the incremental-build and clean behavior of the repository's generated BuildInfo source.
/// </summary>
/// <remarks>
/// The generated project lives under <c>artifacts</c> so that it inherits the repository root
/// <c>Directory.Build.props</c> and <c>Directory.Build.targets</c>.
/// </remarks>
[TestClass]
public sealed class BuildInfoIncrementalCleanTests
{
    private const string TargetFramework = "net8.0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Build_AfterTwoNoOpBuilds_CleanRemovesGeneratedBuildInfo()
    {
        using TestAssetDirectory asset = CreateAsset();
        HashSet<string> binlogPaths = [];

        MSBuildInvocation firstBuild = await BuildAsync(asset, "build-1", restore: true);
        AssertSuccessfulInvocationWithUniqueBinlog(firstBuild, binlogPaths);
        Assert.IsTrue(File.Exists(asset.BuildInfoPath), $"The initial build did not generate '{asset.BuildInfoPath}'.");
        AssertCompilationExecuted(firstBuild);

        MSBuildInvocation secondBuild = await BuildAsync(asset, "build-2", restore: false);
        AssertSuccessfulInvocationWithUniqueBinlog(secondBuild, binlogPaths);
        Assert.IsTrue(File.Exists(asset.BuildInfoPath), $"The generated BuildInfo source disappeared after the first no-op build: '{asset.BuildInfoPath}'.");
        AssertGenerationAndCompilationWereSkipped(secondBuild);

        MSBuildInvocation thirdBuild = await BuildAsync(asset, "build-3", restore: false);
        AssertSuccessfulInvocationWithUniqueBinlog(thirdBuild, binlogPaths);
        Assert.IsTrue(File.Exists(asset.BuildInfoPath), $"The generated BuildInfo source disappeared after the second no-op build: '{asset.BuildInfoPath}'.");
        AssertGenerationAndCompilationWereSkipped(thirdBuild);

        MSBuildInvocation clean = await CleanAsync(asset);
        AssertSuccessfulInvocationWithUniqueBinlog(clean, binlogPaths);
        Assert.IsFalse(File.Exists(asset.BuildInfoPath), $"Clean did not remove the generated BuildInfo source: '{asset.BuildInfoPath}'.");
    }

    private async Task<MSBuildInvocation> BuildAsync(TestAssetDirectory asset, string binlogName, bool restore)
    {
        string binlogPath = Path.Combine(asset.Path, $"{binlogName}.binlog");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.ProjectPath}\" -c {Constants.BuildConfiguration} {(restore ? string.Empty : "--no-restore ")}-v:minimal -nr:false -bl:\"{binlogPath}\"",
            cancellationToken: TestContext.CancellationToken);
        return new MSBuildInvocation(result, binlogPath);
    }

    private async Task<MSBuildInvocation> CleanAsync(TestAssetDirectory asset)
    {
        string binlogPath = Path.Combine(asset.Path, "clean.binlog");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"clean \"{asset.ProjectPath}\" -c {Constants.BuildConfiguration} -v:minimal -nr:false -bl:\"{binlogPath}\"",
            cancellationToken: TestContext.CancellationToken);
        return new MSBuildInvocation(result, binlogPath);
    }

    private static void AssertSuccessfulInvocationWithUniqueBinlog(MSBuildInvocation invocation, HashSet<string> binlogPaths)
    {
        Assert.AreEqual(0, invocation.Result.ExitCode, invocation.Result.ToString());
        Assert.IsTrue(binlogPaths.Add(invocation.BinlogPath), $"MSBuild binlog path was reused: '{invocation.BinlogPath}'.");
        Assert.IsTrue(File.Exists(invocation.BinlogPath), $"MSBuild binlog was not written: '{invocation.BinlogPath}'.");
    }

    private static void AssertCompilationExecuted(MSBuildInvocation invocation)
    {
        SL.Build binlog = BinlogReader.Read(invocation.BinlogPath);
        Assert.HasCount(1, binlog.FindChildrenRecursive<SL.Task>().Where(task => task.Name == "Csc"));
    }

    private static void AssertGenerationAndCompilationWereSkipped(MSBuildInvocation invocation)
    {
        SL.Build binlog = BinlogReader.Read(invocation.BinlogPath);
        SL.Target generateBuildInfo = binlog.FindChildrenRecursive<SL.Target>()
            .Single(target => target.Name == "GenerateVersionSourceFile" && target.Children.Count > 0);

        Assert.HasCount(
            1,
            generateBuildInfo.FindChildrenRecursive<SL.Message>().Where(message => message.Text.Contains(
                "Skipping target \"GenerateVersionSourceFile\" because all output files are up-to-date with respect to the input files.",
                StringComparison.OrdinalIgnoreCase)));
        Assert.IsEmpty(binlog.FindChildrenRecursive<SL.Task>().Where(task => task.Name == "Csc"));
    }

    private static TestAssetDirectory CreateAsset()
    {
        string assetId = $"BuildInfoIncrementalClean{Guid.NewGuid():N}";
        string assetPath = Path.Combine(Constants.Root, "artifacts", "tmp", Constants.BuildConfiguration, "buildInfoIncrementalCleanTests", assetId);
        Directory.CreateDirectory(assetPath);

        string projectPath = Path.Combine(assetPath, $"{assetId}.csproj");
        string intermediateOutputPath = Path.Combine(Constants.Root, "artifacts", "obj", assetId);
        string outputPath = Path.Combine(Constants.Root, "artifacts", "bin", assetId);
        string buildInfoPath = Path.Combine(intermediateOutputPath, Constants.BuildConfiguration, TargetFramework, "BuildInfo.cs");
        string projectContents = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>{TargetFramework}</TargetFramework>
    <GenerateBuildInfo>true</GenerateBuildInfo>
    <IsPackable>false</IsPackable>
    <NoWarn>$(NoWarn);SA1600;SA1633</NoWarn>
  </PropertyGroup>
</Project>
""";
        File.WriteAllText(projectPath, projectContents, Encoding.UTF8);

        string templateContents = """
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/// <summary>
/// Build version generated from the repository template target.
/// </summary>
internal static class BuildInfo
{
    /// <summary>
    /// Gets the generated build version.
    /// </summary>
    internal const string Version = "${Version}";
}
""";
        File.WriteAllText(Path.Combine(assetPath, "BuildInfo.cs.template"), templateContents, Encoding.UTF8);

        return new TestAssetDirectory(assetPath, projectPath, buildInfoPath, intermediateOutputPath, outputPath);
    }

    private sealed class TestAssetDirectory(string path, string projectPath, string buildInfoPath, params string[] additionalPathsToDelete) : IDisposable
    {
        public string Path { get; } = path;

        public string ProjectPath { get; } = projectPath;

        public string BuildInfoPath { get; } = buildInfoPath;

        public void Dispose()
        {
            foreach (string pathToDelete in additionalPathsToDelete.Prepend(Path))
            {
                if (Directory.Exists(pathToDelete))
                {
                    Directory.Delete(pathToDelete, recursive: true);
                }
            }
        }
    }

    private sealed record MSBuildInvocation(DotnetMuxerResult Result, string BinlogPath);
}
