// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class CustomHelpTests : AcceptanceTestBase
{
    private const string AssetName = "CustomHelp";
    private readonly TestAssetFixture _testAssetFixture;

    public CustomHelpTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Help_WhenCustomHelpCapabilityRegistered_OutputCustomHelpContent(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--help");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        // Verify that the custom help message is displayed
        testHostResult.AssertOutputContains("Custom Help Message from CustomTestFramework");
        testHostResult.AssertOutputContains("This is a custom help implementation");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        private const string SourceCode = """
#file CustomHelp.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file Program.cs
using System;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.OutputDevice;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new CustomTestFrameworkCapabilities(),
    (_, _) => new CustomTestFramework());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file CustomTestFramework.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class CustomTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    private readonly IHelpMessageOwnerCapability _helpCapability = new CustomHelpCapability();

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => new[] { _helpCapability };

    public T? GetCapability<T>() where T : ITestFrameworkCapability
    {
        foreach (var capability in Capabilities)
        {
            if (capability is T match)
                return match;
        }
        return default;
    }
}

public class CustomHelpCapability : IHelpMessageOwnerCapability
{
    public Task<bool> DisplayHelpAsync(IOutputDevice outputDevice, 
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> systemCommandLineOptions,
        IReadOnlyCollection<(IExtension Extension, IReadOnlyCollection<CommandLineOption> Options)> extensionsCommandLineOptions)
    {
        var customHelpMessage = """
Custom Help Message from CustomTestFramework

This is a custom help implementation that demonstrates the IHelpMessageOwnerCapability.

Available options:
  --custom-option    A custom option specific to this test framework
  --framework-help   Display framework-specific help

For more information, visit: https://example.com/docs
""";
        
        return outputDevice.DisplayAsync(this, new TextOutputDeviceData(customHelpMessage))
            .ContinueWith(_ => true);
    }
}

public class CustomTestFramework : ITestFramework
{
    public string Uid => nameof(CustomTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => "Custom Test Framework";
    public string Description => "A test framework demonstrating custom help capability";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // This framework doesn't actually run any tests, it's just for help demonstration
        return Task.CompletedTask;
    }
}

#pragma warning restore TPEXP
""";
    }
}