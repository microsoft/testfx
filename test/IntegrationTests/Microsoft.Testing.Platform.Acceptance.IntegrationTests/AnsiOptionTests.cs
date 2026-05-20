// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class AnsiOptionTests : AcceptanceTestBase<AnsiOptionTests.TestAssetFixture>
{
    private const string AssetName = "AnsiOptionTest";

    // ANSI escape character (ESC, 0x1b). Its presence in captured stdout proves the test host emitted
    // raw ANSI escape sequences even though stdout was redirected by the test runner.
    private const string EscapeCharacter = "\u001b";

    [TestMethod]
    [DataRow("on")]
    [DataRow("true")]
    [DataRow("enable")]
    [DataRow("1")]
    public async Task AnsiOption_OnAliases_ForceAnsiOutputEvenWhenRedirected(string value)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync($"--ansi {value}", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);

        // TestHost.ExecuteAsync prepends `--no-ansi --no-progress`. `--ansi <on-alias>` must take precedence
        // over `--no-ansi` and re-enable ANSI escape codes, even though stdout is being redirected to a pipe.
        Assert.IsTrue(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to contain ANSI escape characters when '--ansi {value}' is specified, but got:\n{result.StandardOutput}");
    }

    [TestMethod]
    [DataRow("off")]
    [DataRow("false")]
    [DataRow("disable")]
    [DataRow("0")]
    public async Task AnsiOption_OffAliases_DisableAnsiOutput(string value)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync($"--ansi {value}", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.IsFalse(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to NOT contain ANSI escape characters when '--ansi {value}' is specified, but got:\n{result.StandardOutput}");
    }

    [TestMethod]
    public async Task AnsiOption_Auto_DoesNotProduceAnsiOutputWhenRedirected()
    {
        // `--ansi auto` is the default and means "let the platform decide".
        // It explicitly overrides any preceding `--no-ansi` (TestHost auto-injects `--no-ansi --no-progress`).
        // To make this test deterministic across CI and local runs, we explicitly clear `GITHUB_ACTIONS` and
        // `TF_BUILD` so the platform does NOT short-circuit to `SimpleAnsi`. With those out of the way and
        // stdout redirected to a pipe by the test runner, the platform should pick `AnsiIfPossible` and the
        // detector then falls back to a NonAnsi terminal that does not emit escape codes.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            "--ansi auto",
            environmentVariables: NotInCIEnvironmentVariables,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.IsFalse(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to NOT contain ANSI escape characters when '--ansi auto' is specified and stdout is redirected, but got:\n{result.StandardOutput}");
    }

    [TestMethod]
    public async Task AnsiOption_OnExplicit_AfterNoAnsi_StillForcesAnsiOutput()
    {
        // Regression guard for the rule "any explicit --ansi value overrides --no-ansi", order-independent.
        // We use --ansi on rather than --ansi auto because ForceOn short-circuits both the CI check AND the
        // LLM env-var check in TerminalOutputDevice.InitializeAsync, making the assertion immune to whatever
        // CI / LLM environment a contributor or CI agent might be running from.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            "--no-ansi --ansi on",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to contain ANSI escape characters proving '--ansi on' overrode the preceding '--no-ansi', but got:\n{result.StandardOutput}");
    }

    [TestMethod]
    public async Task AnsiOption_InvalidArgument_FailsCommandLineValidation()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync("--ansi invalid", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        result.AssertOutputContains("--ansi expects a single parameter");
    }

    public TestContext TestContext { get; set; } = null!;

    // Env vars that make the platform consider it NOT to be in CI. Setting empty strings is enough
    // because TerminalOutputDevice compares against the literal "true".
    private static Dictionary<string, string?> NotInCIEnvironmentVariables => new()
    {
        ["GITHUB_ACTIONS"] = string.Empty,
        ["TF_BUILD"] = string.Empty,
    };

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file AnsiOptionTest.csproj
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
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // Emit one passing test so the summary line is rendered with color (DarkGreen "Passed!").
        // The presence of a color-bearing line is what gives us a reliable signal for ANSI vs non-ANSI output.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
