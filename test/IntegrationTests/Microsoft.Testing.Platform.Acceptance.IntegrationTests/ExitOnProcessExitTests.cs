// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class ExitOnProcessExitTests : AcceptanceTestBase
{
    private const string AssetName = "ExecutionTests";
    private readonly TestAssetFixture _testAssetFixture;

    public ExitOnProcessExitTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public void ExitOnProcessExit_Succeed(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);

        // Create the mutex name used to wait for the PID file created by the test host.
        string waitPid = Guid.NewGuid().ToString("N");
        _ = testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string> { { "WaitPid", waitPid } });

        Process? process;
        var startTime = Stopwatch.StartNew();
        while (true)
        {
            Thread.Sleep(1000);

            // Look for the pid file created by the test host.
            var pidFile = Directory.GetFiles(Path.GetDirectoryName(testHost.FullName)!, "PID").ToArray();
            if (pidFile.Length > 0)
            {
                var pid = File.ReadAllText(pidFile[0]);
                if (int.TryParse(pid, out int pidValue))
                {
                    // Create the process object from the test host one.
                    process = Process.GetProcessById(pidValue);
                    break;
                }
            }

            if (startTime.Elapsed.TotalSeconds > 55)
            {
                throw new Exception("Process PID not found in 60 seconds");
            }
        }

        // Create the mutex and unblock the test host.
        using Mutex waitPidMutex = new(true, waitPid);
        startTime = Stopwatch.StartNew();
        while (!process.HasExited)
        {
            if (startTime.Elapsed.TotalSeconds > 55)
            {
                throw new Exception("Process did not exit in 60 seconds");
            }
        }
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string TestCode = """
#file ExecutionTests.csproj
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
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

if (args.Length == 0)
{
    int currentPid = Process.GetCurrentProcess().Id;
    var currentEntryPoint = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location)
        + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty));

    string mutexName = Guid.NewGuid().ToString("N");
    Environment.SetEnvironmentVariable("WaitTestHost", mutexName);
    ProcessStartInfo processStartInfo = new();
    processStartInfo.FileName = currentEntryPoint;
    processStartInfo.Arguments = $"--exit-on-process-exit {currentPid}";
    processStartInfo.UseShellExecute = false;
    var process = Process.Start(processStartInfo);
    while (!Mutex.TryOpenExisting(mutexName, out Mutex? _))
    {
        Thread.Sleep(500);
    }

    Environment.Exit(0);

    return 0;
}
else
{
    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
    builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestAdapter());
    using ITestApplication app = await builder.BuildAsync();
    return await app.RunAsync();
}


public class DummyTestAdapter : ITestFramework, IDataProducer
{
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
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!)!, "PID"), Process.GetCurrentProcess().Id.ToString());
        while (!Mutex.TryOpenExisting(Environment.GetEnvironmentVariable("WaitPid")!, out Mutex? _))
        {
            Thread.Sleep(500);
        }

        using Mutex mutex1 = new(true, Environment.GetEnvironmentVariable("WaitTestHost"));

        Thread.Sleep(60_000);

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }
}
