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
    public async Task AnsiOption_Auto_OverridesNoAnsi_AndFollowsEnvironmentDetection()
    {
        // TestHost.ExecuteAsync auto-injects `--no-ansi --no-progress`. Asserting "no ESC" with
        // `--ansi auto` would not actually prove `auto` won over `--no-ansi`: stdout-redirection
        // alone also produces no ESC. To deterministically prove the override, we force the platform
        // into a CI environment (which makes `auto` map to `SimpleAnsi`, which emits ESC) and clear
        // known LLM env vars (which would otherwise force `NoAnsi`). With `--no-ansi` still on the
        // command line, the only way ESC can appear in the output is if `--ansi auto` overrode it.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            "--ansi auto",
            environmentVariables: InCIEnvironmentVariablesWithLLMCleared,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to contain ANSI escape characters proving '--ansi auto' overrode the auto-injected '--no-ansi' (SimpleAnsi mode in CI), but got:\n{result.StandardOutput}");
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

    [TestMethod]
    public async Task AnsiOption_MissingArgument_FailsCommandLineValidation()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync("--ansi", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);

        // When `--ansi` is supplied without a value, the platform's arity check
        // (ArgumentArity.ExactlyOne) rejects it before the per-occurrence validator runs, so the
        // user sees the generic "expects at least N arguments" message rather than the custom
        // TerminalAnsiOptionInvalidArgument text used for invalid values.
        result.AssertOutputContains("Option '--ansi' from provider 'Terminal test reporter' (UID: TerminalTestReporterCommandLineOptionsProvider) expects at least 1 arguments");
    }

    public TestContext TestContext { get; set; } = null!;

    // Env vars that force the platform to consider it to be in CI while clearing the known LLM env
    // vars (which would otherwise force NoAnsi mode and break tests relying on SimpleAnsi). Only
    // GITHUB_ACTIONS is set to "true" because that is sufficient for CIEnvironmentDetector.
    private static Dictionary<string, string?> InCIEnvironmentVariablesWithLLMCleared => new()
    {
        ["GITHUB_ACTIONS"] = "true",
        ["CLAUDECODE"] = string.Empty,
        ["CLAUDE_CODE_ENTRYPOINT"] = string.Empty,
        ["CURSOR_EDITOR"] = string.Empty,
        ["CURSOR_AI"] = string.Empty,
        ["GEMINI_CLI"] = string.Empty,
        ["GITHUB_COPILOT_CLI_MODE"] = string.Empty,
        ["GH_COPILOT_WORKING_DIRECTORY"] = string.Empty,
        ["COPILOT_CLI"] = string.Empty,
        ["CODEX_CLI"] = string.Empty,
        ["CODEX_SANDBOX"] = string.Empty,
        ["OR_APP_NAME"] = string.Empty,
        ["AMP_HOME"] = string.Empty,
        ["QWEN_CODE"] = string.Empty,
        ["DROID_CLI"] = string.Empty,
        ["OPENCODE_AI"] = string.Empty,
        ["ZED_ENVIRONMENT"] = string.Empty,
        ["ZED_TERM"] = string.Empty,
        ["KIMI_CLI"] = string.Empty,
        ["GOOSE_TERMINAL"] = string.Empty,
        ["CLINE_TASK_ID"] = string.Empty,
        ["ROO_CODE_TASK_ID"] = string.Empty,
        ["WINDSURF_SESSION"] = string.Empty,
        ["AGENT_CLI"] = string.Empty,
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
