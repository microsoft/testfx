// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers <c>ReportEngineBase</c> members that are shared across the CTRF/HTML/JUnit report engines but
/// have no dedicated direct test — only exercised incidentally as a side effect of each concrete engine's
/// own report-generation tests. <c>ReportEngineBase</c> is linked (via <c>&lt;Compile Include&gt;</c>) into
/// each report-engine assembly, so its static <c>GetProvidedFileName</c> helper is resolved through
/// reflection anchored on the CtrfReport assembly (mirroring the pattern used by
/// <see cref="ArtifactPostProcessingHelperTests"/> for other shared-source helpers).
/// </summary>
[TestClass]
public sealed class ReportEngineBaseTests
{
    private static readonly Type ReportEngineBaseType =
        typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.ReportEngineBase", throwOnError: true)!;

    private static readonly MethodInfo GetProvidedFileNameMethod =
        ReportEngineBaseType.GetMethod("GetProvidedFileName", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ReportEngineBase.GetProvidedFileName.");

    private static readonly MethodInfo ResolveOutputPathMethod =
        ReportEngineBaseType.GetMethod(
            "ResolveOutputPath",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(string), typeof(Func<string>)],
            modifiers: null)
        ?? throw new InvalidOperationException("Could not resolve ReportEngineBase.ResolveOutputPath.");

    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<ICommandLineOptions> _commandLineOptionsMock = new();
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IClock> _clockMock = new();
    private readonly Mock<ITestFramework> _testFrameworkMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();

    [TestMethod]
    public void GetProvidedFileName_NonEmptyArray_ReturnsFirstElement()
    {
        string result = InvokeGetProvidedFileName(["first.json", "ignored-second.json"]);

        Assert.AreEqual("first.json", result);
    }

    [TestMethod]
    public void GetProvidedFileName_NullArray_ThrowsUnreachable()
    {
        TargetInvocationException ex = Assert.ThrowsExactly<TargetInvocationException>(() => InvokeGetProvidedFileName(null));

        // ApplicationStateGuard.Unreachable() throws System.Diagnostics.UnreachableException (or its
        // Polyfills-shipped equivalent on non-netcoreapp TFMs) — assert by name so the test doesn't need
        // to reference the internal polyfill type directly.
        Assert.AreEqual("UnreachableException", ex.InnerException!.GetType().Name);
    }

    [TestMethod]
    public void GetProvidedFileName_EmptyArray_ThrowsUnreachable()
    {
        TargetInvocationException ex = Assert.ThrowsExactly<TargetInvocationException>(() => InvokeGetProvidedFileName([]));

        Assert.AreEqual("UnreachableException", ex.InnerException!.GetType().Name);
    }

    [TestMethod]
    public void ResolveOutputPath_FileNameOptionExplicitlySet_ReturnsExplicitPathAndFlag()
    {
        string[]? explicitFileName = ["explicit-name.ctrf.json"];
        _ = _commandLineOptionsMock.Setup(x => x.IsOptionSet(CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName)).Returns(true);
        _ = _commandLineOptionsMock.Setup(x => x.TryGetOptionArgumentList(CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName, out explicitFileName)).Returns(true);

        CtrfReportEngine engine = CreateEngine(CancellationToken.None);
        _ = _configurationMock.SetupGet(x => x[It.IsAny<string>()]).Returns("out");

        (string finalPath, bool wasExplicit) = ((string, bool))ResolveOutputPathMethod.Invoke(
            engine,
            [CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName, (Func<string>)(() => "default.ctrf.json")])!;

        string expectedPath = Path.Combine("out", "explicit-name.ctrf.json");
        Assert.AreEqual(expectedPath, finalPath);
        Assert.IsTrue(wasExplicit);
    }

    [TestMethod]
    public async Task GenerateReportAsync_PreCanceledToken_ThrowsOperationCanceledBeforeWritingAnything()
    {
        // ResolveOutputPath calls _cancellationToken.ThrowIfCancellationRequested() as its very first
        // statement, before consulting the file-name option or touching the file system at all — assert
        // that a pre-canceled token short-circuits the whole report generation with no file-system access.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        CtrfReportEngine engine = CreateEngine(cts.Token);
        _ = _configurationMock.SetupGet(x => x[It.IsAny<string>()]).Returns("out");

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => engine.GenerateReportAsync([]));

        _fileSystemMock.Verify(x => x.CreateDirectory(It.IsAny<string>()), Times.Never);
        _fileSystemMock.Verify(x => x.ExistFile(It.IsAny<string>()), Times.Never);
        _fileSystemMock.Verify(x => x.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>()), Times.Never);
    }

    private static string InvokeGetProvidedFileName(string[]? providedFileName)
        => (string)GetProvidedFileNameMethod.Invoke(null, [providedFileName])!;

    private CtrfReportEngine CreateEngine(CancellationToken cancellationToken = default)
    {
        _ = _environmentMock.SetupGet(x => x.MachineName).Returns("MachineName");
        _ = _environmentMock.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns("user");
        _ = _testApplicationModuleInfoMock.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("TestAppPath");
        _ = _testFrameworkMock.SetupGet(x => x.Uid).Returns("fake-uid");
        _ = _testFrameworkMock.SetupGet(x => x.Version).Returns("0.0.0");
        _ = _testFrameworkMock.SetupGet(x => x.DisplayName).Returns("Fake");

        return new CtrfReportEngine(new(
            _fileSystemMock.Object,
            _testApplicationModuleInfoMock.Object,
            _environmentMock.Object,
            _commandLineOptionsMock.Object,
            _configurationMock.Object,
            _clockMock.Object,
            _testFrameworkMock.Object,
            DateTimeOffset.UtcNow,
            0,
            cancellationToken));
    }
}
