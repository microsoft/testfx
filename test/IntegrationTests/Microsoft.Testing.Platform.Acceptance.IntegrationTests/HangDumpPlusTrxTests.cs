// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Covers the HangDump + TRX combination explicitly required by issue
/// <see href="https://github.com/microsoft/testfx/issues/10792"/>: when HangDump terminates a hung test
/// host, the surviving controller must still recover the completed results into a TRX report with a
/// failed <c>ResultSummary</c> and run diagnostics, exactly like the plain-crash and
/// <c>--timeout</c>-triggered cases already covered by <see cref="TrxTests"/>.
///
/// <see cref="ForwardCompatibilityTests"/> also runs <c>--hangdump --report-trx</c> together, but only on
/// a test host that completes normally, so it never exercises the controller's crash-recovery path — a
/// regression there would go undetected. This class instead simulates a real hang (one test completes,
/// the next sleeps well past <c>--hangdump-timeout</c>) so HangDump actually kills the host.
/// </summary>
[TestClass]
public sealed class HangDumpPlusTrxTests : AcceptanceTestBase<HangDumpPlusTrxTests.TestAssetFixture>
{
    [TestMethod]
    public async Task HangDumpPlusTrx_WhenTestHostHangs_RecoversCompletedResultsAndFailedResultSummary()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        string fileName = $"{Guid.NewGuid():N}.trx";
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --report-trx --report-trx-filename {fileName} --results-directory \"{resultDirectory}\"",
            new Dictionary<string, string?>
            {
                // Test1 completes quickly, well before the streaming store's 500ms default flush
                // interval and the 8s hangdump timeout, so it is durably recorded in the sidecar. Test2
                // then sleeps far longer than the hangdump timeout so HangDump actually kills the host.
                { "SLEEPTIMEMS1", "1000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        // Two copies of the dump are expected: the original HangDump wrote, plus TRX's own copy of it
        // into the deployment "In" attachment folder (the same attachment-copying behavior asserted in
        // Trx_WhenReportTrxAndResultsDirectoryAreSpecifiedWithArtifact_ArtifactIsCopiedUnderRelativeResultsDirectory).
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "*.dmp", SearchOption.AllDirectories);
        Assert.IsGreaterThan(0, dumpFiles.Length, $"Expected at least one dump file. Found: {Environment.NewLine}{string.Join(Environment.NewLine, dumpFiles)}{Environment.NewLine}{testHostResult}");

        string[] trxFiles = Directory.GetFiles(resultDirectory, fileName, SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");

        string trxContent = File.ReadAllText(trxFiles[0]);

        // The controller's crash-recovery path (OnTestHostProcessExitedAsync, taken whenever the host
        // exits before it can report its own TRX file name) always reports a failed run, regardless of
        // whether HangDump or an actual crash caused the termination.
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);

        // Direct proof of recovery: Test1 completed and was streamed to the sidecar before the hang, so
        // it must appear in the TRX even though the process was killed mid-run.
        Assert.Contains("Test1", trxContent, trxContent);

        // Test2 never completed (the sleep that trips the hangdump timeout runs after it, before the
        // update publishes), so it must be absent from the recovered results.
        Assert.DoesNotContain("Test2", trxContent, trxContent);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "HangDumpPlusTrxTest";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        private const string SourceCode = """
#file HangDumpPlusTrxTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using System.Globalization;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_, _) => new DummyTestFramework());
        builder.AddHangDumpProvider();
        builder.AddTrxReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS1")!, CultureInfo.InvariantCulture));

        var test1Identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test1", 0, Array.Empty<string>(), string.Empty);
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, test1Identifier),
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS2")!, CultureInfo.InvariantCulture));

        var test2Identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test2", 0, Array.Empty<string>(), string.Empty);
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test2",
            DisplayName = "Test2",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, test2Identifier),
        }));

        context.Complete();
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
