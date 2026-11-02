// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

[TestClass]
public sealed class DotnetTestPipeStartupErrorTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = nameof(DotnetTestPipeStartupErrorTests);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, "--unknown-option", "Unknown option '--unknown-option'", DisplayName = "StartupError_UnknownOption")]
    [DataRow(false, "--report-gh", "Option '--report-gh' is provided by the 'Microsoft.Testing.Extensions.GitHubActionsReport' extension. Add a package reference to use it.", DisplayName = "StartupError_MissingExtension")]
    [DataRow(true, "", "Option '--my-option' is declared by multiple providers: 'ProviderA', 'ProviderB'", DisplayName = "StartupError_DuplicateProviders")]
    [DataRow(false, "--config-file \"$ConfigFile$\"", "The entry 'timeout' under section 'commandLineOptions' in the testconfig.json file ('$ConfigFile$') must be either a scalar value (string, number, boolean) or an array of scalar values. Nested objects are not supported.", DisplayName = "StartupError_MalformedConfiguration")]
    public async Task DotnetTest_CommandLineValidationFailure_ShowsHostDiagnostic(bool duplicateProviders, string arguments, string expectedDiagnostic)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$RegisterProviders$", duplicateProviders
                    ? """
                        builder.CommandLine.AddProvider(() => new OptionProvider("ProviderA"));
                        builder.CommandLine.AddProvider(() => new OptionProvider("ProviderB"));
                        """
                    : string.Empty));
        await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -c Release",
            cancellationToken: TestContext.CancellationToken);

        string configFile = Path.Combine(asset.TargetAssetPath, "invalid.testconfig.json");
        arguments = arguments.Replace("$ConfigFile$", configFile, StringComparison.Ordinal);
        expectedDiagnostic = expectedDiagnostic.Replace("$ConfigFile$", configFile, StringComparison.Ordinal);
        var testHost = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        TestHostResult directResult = await testHost.ExecuteAsync(arguments, cancellationToken: TestContext.CancellationToken);
        directResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        directResult.AssertOutputContains(expectedDiagnostic);

        string modulePath = Path.Combine("bin", "Release", TargetFrameworks.NetCurrent, $"{AssetName}.dll");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --test-modules \"{modulePath}\" --root-directory \"{asset.TargetAssetPath}\" {arguments}",
            workingDirectory: asset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        result.AssertOutputContains(expectedDiagnostic);
        Assert.DoesNotContain($"Usage {AssetName}", result.StandardOutput);
    }

    private const string SourceCode = """
        #file DotnetTestPipeStartupErrorTests.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
            <OutputType>Exe</OutputType>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
          </ItemGroup>
        </Project>

        #file Program.cs
        using Microsoft.Testing.Platform.Builder;
        using Microsoft.Testing.Platform.Capabilities.TestFramework;
        using Microsoft.Testing.Platform.CommandLine;
        using Microsoft.Testing.Platform.Extensions;
        using Microsoft.Testing.Platform.Extensions.CommandLine;
        using Microsoft.Testing.Platform.Extensions.TestFramework;

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DummyTestFramework());
        $RegisterProviders$
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();

        public sealed class OptionProvider(string uid) : ICommandLineOptionsProvider
        {
            public string Uid => uid;
            public string Version => "1.0.0";
            public string DisplayName => Uid;
            public string Description => Uid;
            public Task<bool> IsEnabledAsync() => Task.FromResult(true);
            public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
                [new CommandLineOption("my-option", "Declared by two providers.", ArgumentArity.Zero, false)];
            public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption option, string[] arguments) =>
                ValidationResult.ValidTask;
            public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions options) =>
                ValidationResult.ValidTask;
        }

        public sealed class DummyTestFramework : ITestFramework
        {
            public string Uid => nameof(DummyTestFramework);
            public string Version => "1.0.0";
            public string DisplayName => Uid;
            public string Description => Uid;
            public Task<bool> IsEnabledAsync() => Task.FromResult(true);
            public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) =>
                Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
            public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) =>
                Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
            public Task ExecuteRequestAsync(ExecuteRequestContext context)
            {
                context.Complete();
                return Task.CompletedTask;
            }
        }

        #file invalid.testconfig.json
        {
          "commandLineOptions": {
            "timeout": {}
          }
        }
        """;
}
