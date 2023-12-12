// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class EnvironmentVariablesConfigurationProviderTests : BaseAcceptanceTests
{
    private readonly EnvironmentVariablesConfigurationProviderFixture _environmentVariablesConfigurationProviderFixture;

    public EnvironmentVariablesConfigurationProviderTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture,
        EnvironmentVariablesConfigurationProviderFixture environmentVariablesConfigurationProviderFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _environmentVariablesConfigurationProviderFixture = environmentVariablesConfigurationProviderFixture;
    }

    private const string AssetName = "EnvironmentVariablesConfigurationProvider";

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task DefaultEnabledEnvironmentVariablesConfiguration_SetEnvironmentVariable_ShouldSucceed(string currentTfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_environmentVariablesConfigurationProviderFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { "DefaultEnableConfigurationSource", "true" },
                { "MyValue", "MyVal" },
                { "MYENVVAR__MYPROP1__MYPROP2", "MyVal" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.Success);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task EnabledEnvironmentVariablesConfiguration_SetEnvironmentVariable_ShouldSucceed(string currentTfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_environmentVariablesConfigurationProviderFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { "EnableConfigurationSource", "true" },
                { "MyValue", "MyVal" },
                { "MYENVVAR__MYPROP1__MYPROP2", "MyVal" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.Success);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task DisabledEnvironmentVariablesConfiguration_SetEnvironmentVariable_ShouldSucceed(string currentTfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_environmentVariablesConfigurationProviderFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { "EnableConfigurationSource", "false" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.Success);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class EnvironmentVariablesConfigurationProviderFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _testAsset;

        public string TargetAssetPath => _testAsset is null ? throw new ArgumentNullException(nameof(TestAsset)) : _testAsset.TargetAssetPath;

        public EnvironmentVariablesConfigurationProviderFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync(AssetName, Sources.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToJoinedFrameworks()));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string Sources = """
#file EnvironmentVariablesConfigurationProvider.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>tfms</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
    <PackageReference Include="Microsoft.Testing.Platform.Extensions" Version="[1.0.0-*,)" />
  </ItemGroup>
</Project>

#file Program.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        bool defaultEnabled = Environment.GetEnvironmentVariable("DefaultEnableConfigurationSource")! == "true";
        Console.WriteLine($"DefaultEnableConfigurationSource: {defaultEnabled}");

        ITestApplicationBuilder? builder = null;
        if (defaultEnabled)
        {
            Environment.SetEnvironmentVariable("EnableConfigurationSource", "true");
            builder = await TestApplication.CreateBuilderAsync(args);
        }
        else
        {
            builder = await TestApplication.CreateBuilderAsync(args);
        }

        builder.TestHost.AddTestApplicationLifecycleCallbacks(sp => new Hooks(sp));
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

internal class Hooks : ITestApplicationLifecycleCallbacks
{
    private readonly IServiceProvider _serviceProvider;

    public string Uid => nameof(Hooks);

    public string Version => "1.0.0";

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Hooks(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task AfterRunAsync(int returnValue, CancellationToken _) => Task.CompletedTask;

    public Task BeforeRunAsync(CancellationToken _)
    {
        bool enabled = Environment.GetEnvironmentVariable("EnableConfigurationSource")! == "true";

        Console.WriteLine($"EnableConfigurationSource: {enabled}");

        var myValue = Environment.GetEnvironmentVariable("MyValue")!;
        Console.WriteLine($"MyValue: {myValue}");

        var propValue = _serviceProvider.GetConfiguration()["MYENVVAR:MYPROP1:MYPROP2"];

        if (enabled && propValue != myValue)
        {
            throw new Exception($"Expected MYENVVAR:MYPROP1:MYPROP2 to be '{myValue}', but was '{propValue}'");
        }

        if (!enabled && propValue is not null)
        {
            throw new Exception($"Expected MYENVVAR:MYPROP1:MYPROP2 to be null, but was '{propValue}'");
        }

        return Task.CompletedTask;
    }
}

public class DummyTestAdapter : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode() 
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty())
        }));

        context.Complete();
    }
}
""";
}
