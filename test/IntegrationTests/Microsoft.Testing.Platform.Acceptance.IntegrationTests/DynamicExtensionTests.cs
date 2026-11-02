// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for dynamically resolved extensions
/// (see <c>docs/RFCs/023-Dynamic-Extension-Loading.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// The manifests point at the test application's own assembly. That is unusual for a real deployment, but it
/// keeps the asset small while still covering discovery, manifest parsing, assembly loading, hook lookup and
/// registration end to end on every target framework.
/// </para>
/// <para>
/// What it proves differs by target framework, because isolation does. On .NET the assembly is loaded into a
/// dedicated <c>AssemblyLoadContext</c>, so the hook runs from a *second* copy of that assembly and the
/// <c>ITestApplicationBuilder</c> it receives can only work if the platform assembly is shared with the default
/// context as designed. On .NET Framework the loader uses <c>Assembly.LoadFrom</c> and reports
/// <c>IsIsolated == false</c>, so the net462 rows cover discovery and hook registration only, not isolation.
/// Cross-context isolation with a genuinely separate extension assembly is covered by
/// <see cref="DynamicExtensionIsolationTests"/>, which is .NET-only for the same reason.
/// </para>
/// </remarks>
[TestClass]
public sealed class DynamicExtensionTests : AcceptanceTestBase<DynamicExtensionTests.TestAssetFixture>
{
    private const string AssetName = "DynamicExtensionTest";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task EnabledManifest_RegistersTheExtension(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
        testHostResult.AssertOutputContains(TestAssetFixture.EnabledHookMarker);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task DisabledManifestEntry_IsNotRegistered(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
        testHostResult.AssertOutputDoesNotContain(TestAssetFixture.DisabledHookMarker);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task RegisteredExtension_IsVisibleInInfo(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions --info", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Proves the hook really registered into the builder rather than merely being invoked.
        testHostResult.AssertOutputContains(TestAssetFixture.CommandLineProviderDisplayName);
        testHostResult.AssertOutputContains($"--{TestAssetFixture.CommandLineOptionName}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WithoutTheOptIn_TheManifestIsIgnoredEntirely(string tfm)
    {
        // Off by default is a predictability decision rather than a security control (see the RFC's Trust
        // section): a manifest sitting in an output directory must not change how a run behaves unless the
        // run asked for it.
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
        testHostResult.AssertOutputDoesNotContain(TestAssetFixture.EnabledHookMarker);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task LoadedExtensions_AreReportedOnTheOutput(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        // Loading is never silent: the user sees what was loaded and from where, without having to turn on
        // diagnostic logging to find out.
        testHostResult.AssertOutputContains("Contoso policy");
        testHostResult.AssertOutputContains(".testingplatformextensions.json");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string EnabledHookMarker = "CONTOSO_DYNAMIC_HOOK_RAN";
        public const string DisabledHookMarker = "CONTOSO_DISABLED_HOOK_RAN";
        public const string CommandLineProviderDisplayName = "Contoso policy options";
        public const string CommandLineOptionName = "contoso-policy";

        private const string SourceCode = $$"""
#file DynamicExtensionTest.csproj
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

    <ItemGroup>
        <Content Include="contoso.net.testingplatformextensions.json"
                 Condition=" '$(TargetFrameworkIdentifier)' == '.NETCoreApp' "
                 CopyToOutputDirectory="PreserveNewest" />
        <Content Include="contoso.netfx.testingplatformextensions.json"
                 Condition=" '$(TargetFrameworkIdentifier)' == '.NETFramework' "
                 CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>

#file contoso.net.testingplatformextensions.json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testingplatformextensions.schema.json",
  "extensions": [
    {
      "id": "3F1E4C6A-6E4B-4C0E-9C1E-0F2E1C6A3F1E",
      "displayName": "Contoso policy",
      "assemblyPath": "DynamicExtensionTest.dll",
      "typeFullName": "Contoso.TestingPlatformBuilderHook"
    },
    {
      "id": "9B2D5A7C-1D3E-4F5A-8B6C-7D8E9F0A1B2C",
      "displayName": "Contoso disabled policy",
      "assemblyPath": "DynamicExtensionTest.dll",
      "typeFullName": "Contoso.DisabledTestingPlatformBuilderHook",
      "enabled": false
    }
  ]
}

#file contoso.netfx.testingplatformextensions.json
{
  "extensions": [
    {
      "id": "3F1E4C6A-6E4B-4C0E-9C1E-0F2E1C6A3F1E",
      "displayName": "Contoso policy",
      "assemblyPath": "DynamicExtensionTest.exe",
      "typeFullName": "Contoso.TestingPlatformBuilderHook"
    },
    {
      "id": "9B2D5A7C-1D3E-4F5A-8B6C-7D8E9F0A1B2C",
      "displayName": "Contoso disabled policy",
      "assemblyPath": "DynamicExtensionTest.exe",
      "typeFullName": "Contoso.DisabledTestingPlatformBuilderHook",
      "enabled": false
    }
  ]
}

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

namespace Contoso
{
    public static class TestingPlatformBuilderHook
    {
        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            Console.WriteLine("{{EnabledHookMarker}}");
            builder.CommandLine.AddProvider(() => new ContosoCommandLineProvider());
        }
    }

    public static class DisabledTestingPlatformBuilderHook
    {
        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
            => Console.WriteLine("{{DisabledHookMarker}}");
    }

    public sealed class ContosoCommandLineProvider : ICommandLineOptionsProvider
    {
        public string Uid => "Contoso.PolicyOptions";

        public string Version => "1.0.0";

        public string DisplayName => "{{CommandLineProviderDisplayName}}";

        public string Description => "Contoso policy options.";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
            => new[] { new CommandLineOption("{{CommandLineOptionName}}", "The Contoso policy to apply.", ArgumentArity.ExactlyOne, false) };

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
            => ValidationResult.ValidTask;

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
            => ValidationResult.ValidTask;
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
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
