// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Baseline tests for the <c>--server dotnettestcli</c> pipe protocol that lock down today's
/// behavior so subsequent phases (quiet output device under pipe mode, protocol 1.0.1, live log
/// and stream forwarding) can be reviewed as targeted diffs against these assertions.
/// <para>
/// Tracks <a href="https://github.com/dotnet/sdk/issues/51615">dotnet/sdk#51615</a> and the
/// related <a href="https://github.com/microsoft/testfx/issues/7161">microsoft/testfx#7161</a>.
/// </para>
/// </summary>
[TestClass]
public class DotnetTestPipeBaselineTests : AcceptanceTestBase<DotnetTestPipeBaselineTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeBaselineTest";

    private static readonly Regex BannerRegex = new(@"Microsoft\.Testing\.Platform v.+ \[.+\]", RegexOptions.Compiled);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DotnetTestPipe_TestAppAdvertisesAndNegotiatesProtocolV100_Today()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(result.ReceivedHandshake, "Test app never sent a handshake message.");
        Assert.IsNotNull(result.SentHandshakeReply);

        Assert.IsTrue(
            result.ReceivedHandshake.TryGetValue(DotnetTestPipeProtocol.HandshakeProperties.SupportedProtocolVersions, out string? appVersions),
            "Handshake from test app is missing SupportedProtocolVersions.");
        Assert.AreEqual(
            FakeDotnetTestSdk.DefaultSupportedProtocolVersions,
            appVersions,
            "BASELINE: today the test app advertises only protocol 1.0.0. When this assertion " +
            "starts failing, it means the testfx side has been bumped to 1.0.1+ and the version " +
            "negotiation tests should take over (Phase 2 of dotnet/sdk#51615).");

        Assert.AreEqual(
            FakeDotnetTestSdk.DefaultSupportedProtocolVersions,
            result.NegotiatedProtocolVersion,
            "Fake SDK should have selected '1.0.0' from the test app's advertised list.");
    }

    [TestMethod]
    public async Task DotnetTestPipe_EmitsTestSessionStartAndEnd()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost, cancellationToken: TestContext.CancellationToken);

        byte[] sessionEventTypes =
        [
            .. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestSessionEvent)
                .Select(m => DotnetTestPipeProtocol.DecodeTestSessionEventBody(m.Body).SessionType)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
        ];

        Assert.HasCount(2, sessionEventTypes, $"Expected exactly two session events; got [{string.Join(", ", sessionEventTypes)}].");
        Assert.AreEqual(DotnetTestPipeProtocol.SessionEventTypes.TestSessionStart, sessionEventTypes[0]);
        Assert.AreEqual(DotnetTestPipeProtocol.SessionEventTypes.TestSessionEnd, sessionEventTypes[1]);
    }

    /// <summary>
    /// BASELINE: under <c>--server dotnettestcli</c> the test app today emits nothing to stdout/stderr
    /// for a passing run (the MTP banner is already silenced via <c>_isServerMode</c> in
    /// <c>TerminalOutputDevice</c>, and our <c>DummyTestFramework</c> writes nothing of its own).
    /// This test pins that contract so future changes can't accidentally start leaking output that
    /// would compete with what the SDK is also rendering (the original symptom behind
    /// dotnet/sdk#51615 and microsoft/testfx#7161). When this test fails, evaluate whether the
    /// added stdout/stderr writes should instead flow through the pipe as structured messages.
    /// </summary>
    [TestMethod]
    public async Task DotnetTestPipe_ChildEmitsNoStdoutOrStderrForPassingRun_Baseline()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            testHost, cancellationToken: TestContext.CancellationToken);

        Assert.DoesNotMatchRegex(
            BannerRegex.ToString(),
            result.TestHostResult.StandardOutput,
            $"BASELINE: under --server dotnettestcli the test app already silences its banner. " +
            $"If this fails, something started re-emitting the banner under pipe mode.{Environment.NewLine}" +
            $"Captured stdout:{Environment.NewLine}{result.TestHostResult.StandardOutput}");

        Assert.AreEqual(
            string.Empty,
            result.TestHostResult.StandardOutput.Trim(),
            $"BASELINE: today the test app emits nothing to stdout under pipe mode for a no-op " +
            $"DummyTestFramework run. New text leaking here likely belongs in a pipe message " +
            $"(Phase 2 of dotnet/sdk#51615 adds a LogMessage channel for this).{Environment.NewLine}" +
            $"Captured stdout:{Environment.NewLine}{result.TestHostResult.StandardOutput}");

        Assert.AreEqual(
            string.Empty,
            result.TestHostResult.StandardError.Trim(),
            $"BASELINE: today the test app emits nothing to stderr under pipe mode for a passing run.{Environment.NewLine}" +
            $"Captured stderr:{Environment.NewLine}{result.TestHostResult.StandardError}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetCode = """
#file DotnetTestPipeBaselineTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        context.Complete();
        return Task.CompletedTask;
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
