﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ExitOnProcessExitTests : AcceptanceTestBase<ExitOnProcessExitTests.TestAssetFixture>
{
    private const string AssetName = "ExecutionTests";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public void ExitOnProcessExit_Succeed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        // Create the mutex name used to wait for the PID file created by the test host.
        string waitPid = Guid.NewGuid().ToString("N");
        _ = testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string?> { { "WaitPid", waitPid } });

        Process? process;
        var startTime = Stopwatch.StartNew();
        while (true)
        {
            Thread.Sleep(500);

            // Look for the pid file created by the test host.
            string[] pidFile = [.. Directory.GetFiles(Path.GetDirectoryName(testHost.FullName)!, "PID")];
            if (pidFile.Length > 0)
            {
                string pid = File.ReadAllText(pidFile[0]);
                if (int.TryParse(pid, out int pidValue))
                {
                    // Create the process object from the test host one.
                    process = Process.GetProcessById(pidValue);
                    break;
                }
            }

            if (startTime.Elapsed.TotalSeconds > 60)
            {
                throw new Exception("Process PID not found in 60 seconds");
            }
        }

        // Create the mutex and unblock the test host.
        using Mutex waitPidMutex = new(true, waitPid);
        startTime = Stopwatch.StartNew();
        while (!process.HasExited)
        {
            Thread.Sleep(1000);
            if (startTime.Elapsed.TotalSeconds > 60)
            {
                throw new Exception("Process did not exit in 60 seconds");
            }
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
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

if (!args.Contains("--exit-on-process-exit"))
{
    int currentPid = Process.GetCurrentProcess().Id;
    var currentEntryPoint = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location)
        + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty));

    string mutexName = Guid.NewGuid().ToString("N");
    Environment.SetEnvironmentVariable("WaitTestHost", mutexName);
    ProcessStartInfo processStartInfo = new();
    processStartInfo.FileName = currentEntryPoint;
    processStartInfo.Arguments = $"--exit-on-process-exit {currentPid} --no-progress --no-ansi";
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
    builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
    using ITestApplication app = await builder.BuildAsync();
    return await app.RunAsync();
}


public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

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
