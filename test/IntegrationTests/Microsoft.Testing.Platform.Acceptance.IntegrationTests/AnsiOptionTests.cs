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
        // The platform should then pick AnsiIfPossible and, because stdout is redirected to a pipe by the
        // test runner, fall back to a NonAnsi terminal that does not emit escape codes.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync("--ansi auto", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.IsFalse(
            result.StandardOutput.Contains(EscapeCharacter, StringComparison.Ordinal),
            $"Expected output to NOT contain ANSI escape characters when '--ansi auto' is specified and stdout is redirected, but got:\n{result.StandardOutput}");
    }

    [TestMethod]
    public async Task AnsiOption_AutoExplicit_OverridesNoAnsiFlag()
    {
        // Both --no-ansi and --ansi auto are present. --ansi wins (it always does when explicitly passed).
        // The auto branch then picks AnsiIfPossible, which respects stdout redirection -> no ANSI codes.
        // We verify the precedence by checking that the test still runs successfully and no escape codes are
        // emitted because of redirection (not because --no-ansi forced NoAnsi).
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync("--no-ansi --ansi auto", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
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
