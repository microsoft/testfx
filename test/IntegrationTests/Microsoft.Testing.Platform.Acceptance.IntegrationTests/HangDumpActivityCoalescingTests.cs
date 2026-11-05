// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class HangDumpActivityCoalescingTests : AcceptanceTestBase<HangDumpActivityCoalescingTests.TestAssetFixture>
{
    private const int ResultCount = 5000;
    private static readonly TimeSpan ActivitySignalInterval = TimeSpan.FromSeconds(1);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("net8.0", false, DisplayName = "net8.0: HangDump only")]
    [DataRow("net8.0", true, DisplayName = "net8.0: HangDump with TRX")]
    [DataRow("net10.0", false, DisplayName = "net10.0: HangDump only")]
    [DataRow("net10.0", true, DisplayName = "net10.0: HangDump with TRX")]
    public async Task HangDump_BurstOfResults_CoalescesAcknowledgedActivityBeforeExit(string tfm, bool reportTrx)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        string trxOptions = reportTrx ? "--report-trx --report-trx-filename results.trx" : string.Empty;

        var stopwatch = Stopwatch.StartNew();
        TestHostResult result = await testHost.ExecuteAsync(
            $"--hangdump --no-banner --diagnostic --diagnostic-verbosity trace --diagnostic-output-directory \"{resultDirectory}\" --results-directory \"{resultDirectory}\" {trxOptions}",
            cancellationToken: TestContext.CancellationToken);
        stopwatch.Stop();

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: ResultCount, skipped: 0);
        Assert.IsEmpty(Directory.GetFiles(resultDirectory, "*.dmp", SearchOption.AllDirectories));

        string[] diagnostics = Directory.GetFiles(resultDirectory, "*.diag", SearchOption.AllDirectories);
        Assert.IsNotEmpty(diagnostics, result.ToString());
        string[] messages = [.. diagnostics.SelectMany(File.ReadAllLines)];
        int sentSignals = messages.Count(message => message.Contains("Activity signal sent.", StringComparison.Ordinal));
        int receivedSignals = messages.Count(message => message.Contains("Activity signal received by the test host", StringComparison.Ordinal));

        // The default timeout caps the loop interval at one second. The whole launch duration is a
        // conservative bound on loop iterations, plus the final flush; this is an IPC-rate assertion,
        // not a wall-clock performance limit.
        int maximumSignals = (int)Math.Floor(stopwatch.Elapsed.TotalSeconds / ActivitySignalInterval.TotalSeconds) + 1;
        TestContext.WriteLine($"{ResultCount} results; {sentSignals} sent signals; {receivedSignals} acknowledged signals; maximum {maximumSignals}.");
        Assert.IsGreaterThan(0, sentSignals, "Pending activity must be flushed even when the session finishes before the first interval.");
        Assert.AreEqual(sentSignals, receivedSignals, "Every activity request must be acknowledged before the host exits.");
        Assert.IsLessThanOrEqualTo(maximumSignals, sentSignals, "Results must not each pay an IPC round trip.");

        if (reportTrx)
        {
            var report = XDocument.Load(Path.Combine(resultDirectory, "results.trx"));
            XNamespace ns = report.Root!.Name.Namespace;
            XElement counters = report.Descendants(ns + "Counters").Single();
            Assert.AreEqual(ResultCount.ToString(CultureInfo.InvariantCulture), counters.Attribute("passed")!.Value);
            Assert.HasCount(ResultCount, report.Descendants(ns + "UnitTestResult"));
        }
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string AssetName = "HangDumpActivityCoalescing";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, Sources
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$ResultCount$", ResultCount.ToString(CultureInfo.InvariantCulture)));

        private const string Sources = """
#file HangDumpActivityCoalescing.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new TestFrameworkCapabilities(new TrxCapability()),
    (_, _) => new BurstTestFramework());
builder.AddHangDumpProvider();
builder.AddTrxReportProvider();
using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();

public sealed class TrxCapability : ITrxReportCapability
{
    public bool IsSupported => true;

    public void Enable()
    {
    }
}

public sealed class BurstTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(BurstTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => nameof(BurstTestFramework);

    public string Description => nameof(BurstTestFramework);

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        for (int i = 0; i < $ResultCount$; i++)
        {
            string name = $"Test{i}";
            var identifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, nameof(BurstTestFramework), name, 0, [], string.Empty);
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode
            {
                Uid = name,
                DisplayName = name,
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, identifier),
            }));
        }

        context.Complete();
    }
}
""";
    }
}
