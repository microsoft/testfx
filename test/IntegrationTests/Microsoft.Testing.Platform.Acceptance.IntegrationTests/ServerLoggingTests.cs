// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

using MSTest.Acceptance.IntegrationTests.Messages.V100;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed partial class ServerLoggingTests : ServerModeTestsBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public ServerLoggingTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    public async Task RunningInServerJsonRpcModeShouldHaveOutputDeviceLogsPushedToTestExplorer()
    {
        string tfm = TargetFrameworks.NetCurrent.Arguments;
        string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "ServerLoggingTests", tfm);
        using TestingPlatformClient jsonClient = await StartAsServerAndConnectToTheClientAsync(testHost);
        LogsCollector logs = new();
        jsonClient.RegisterLogListener(logs);

        InitializeResponse initializeResponseArgs = await jsonClient.Initialize();

        TestNodeUpdateCollector discoveryCollector = new();
        ResponseListener discoveryListener = await jsonClient.DiscoverTests(Guid.NewGuid(), discoveryCollector.CollectNodeUpdates);

        TestNodeUpdateCollector runCollector = new();
        ResponseListener runListener = await jsonClient.RunTests(Guid.NewGuid(), runCollector.CollectNodeUpdates);

        await Task.WhenAll(discoveryListener.WaitCompletion(), runListener.WaitCompletion());
        Assert.IsFalse(logs.Count == 0, $"Logs are empty");
        string logsString = string.Join(Environment.NewLine, logs.Select(l => l.ToString()));
        string logPath = LogFilePathRegex().Match(logsString).Groups[1].Value;
        string port = PortRegex().Match(logsString).Groups[1].Value;

        Assert.AreEqual(
            $$"""
            Log { LogLevel = Information, Message = Connecting to client host '127.0.0.1' port '{{port}}' }
            Log { LogLevel = Trace, Message = Starting test session. Log file path is '{{logPath}}'. }
            Log { LogLevel = Error, Message = System.Exception: This is an exception output }
            Log { LogLevel = Error, Message =    This is a red output with padding set to 3 }
            Log { LogLevel = Warning, Message =   This is a yellow output with padding set to 2 }
            Log { LogLevel = Information, Message =  This is a blue output with padding set to 1 }
            Log { LogLevel = Information, Message = This is normal text output. }
            Log { LogLevel = Trace, Message = Finished test session }
            Log { LogLevel = Trace, Message = Starting test session. Log file path is '{{logPath}}'. }
            Log { LogLevel = Error, Message = System.Exception: This is an exception output }
            Log { LogLevel = Error, Message =    This is a red output with padding set to 3 }
            Log { LogLevel = Warning, Message =   This is a yellow output with padding set to 2 }
            Log { LogLevel = Information, Message =  This is a blue output with padding set to 1 }
            Log { LogLevel = Information, Message = This is normal text output. }
            Log { LogLevel = Trace, Message = Finished test session }
            """, logsString);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "TestAssetFixture";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        private const string Sources = """
#file ServerLoggingTests.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs

using System;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, serviceProvider) => new DummyTestAdapter(serviceProvider));
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestAdapter : ITestFramework, IDataProducer, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public DummyTestAdapter(IServiceProvider serviceProvider)
    {
        _outputDevice = serviceProvider.GetOutputDevice();
    }

    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await _outputDevice.DisplayAsync(this, new ExceptionOutputDeviceData(new Exception("This is an exception output")));
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("This is a red output with padding set to 3")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red },
            Padding = 3,
        });

        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("This is a yellow output with padding set to 2")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow },
            Padding = 2,
        });

        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("This is a blue output with padding set to 1")
        {
            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Blue },
            Padding = 1,
        });

        await _outputDevice.DisplayAsync(this, new TextOutputDeviceData("This is normal text output."));
        context.Complete();
    }
}
""";
    }

    [GeneratedRegex("Connecting to client host '127.0.0.1' port '(\\d+)'")]
    private static partial Regex PortRegex();

    [GeneratedRegex("The log file path is '(.+?)'")]
    private static partial Regex LogFilePathRegex();
}
