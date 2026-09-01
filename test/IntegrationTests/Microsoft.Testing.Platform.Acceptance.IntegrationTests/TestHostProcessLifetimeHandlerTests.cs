// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestHostProcessLifetimeHandlerTests : AcceptanceTestBase<TestHostProcessLifetimeHandlerTests.TestAssetFixture>
{
    private const string AssetName = "TestHostProcessLifetimeHandler";
    private static readonly TimeSpan RendezvousWaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumFinalizationTime = TimeSpan.FromSeconds(5);

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task All_Interface_Methods_ShouldBe_Invoked(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.AreEqual("TestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "BeforeTestHostProcessStartAsync.txt")));
        Assert.AreEqual("TestHostProcessLifetimeHandler.OnTestHostProcessStartedAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "OnTestHostProcessStartedAsync.txt")));
        Assert.AreEqual("TestHostProcessLifetimeHandler.OnTestHostProcessExitedAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "OnTestHostProcessExitedAsync.txt")));
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Timeout_FinalizesProcessLifetimeHandlerWithUncanceledToken(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string finalizationFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.txt");

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--timeout 500ms",
            new()
            {
                ["BLOCK_UNTIL_TIMEOUT"] = "1",
                ["FINALIZATION_FILE"] = finalizationFile,
                ["SKIP_FIXED_LIFECYCLE_FILES"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.AreEqual(bool.FalseString, File.ReadAllText(finalizationFile));
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Timeout_BoundsBlockingFinalizationWithoutDisposingRunningHandler(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string finalizationStartedFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.started.txt");
        string disposalFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.disposed.txt");
        string executionReadyFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.ready.txt");
        string executionReleaseFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.release.txt");
        using var rendezvousTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        rendezvousTimeout.CancelAfter(RendezvousWaitTimeout);

        Task<TestHostResult> executionTask = testHost.ExecuteAsync(
            "--timeout 500ms",
            new()
            {
                ["BLOCK_UNTIL_TIMEOUT"] = "1",
                ["BLOCK_FINALIZATION"] = "1",
                ["FINALIZATION_STARTED_FILE"] = finalizationStartedFile,
                ["DISPOSAL_FILE"] = disposalFile,
                ["EXECUTION_READY_FILE"] = executionReadyFile,
                ["EXECUTION_RELEASE_FILE"] = executionReleaseFile,
                ["TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS"] = "0.5",
                ["SKIP_FIXED_LIFECYCLE_FILES"] = "1",
            },
            cancellationToken: rendezvousTimeout.Token);

        await WaitForFileAsync(executionReadyFile, rendezvousTimeout.Token);
        rendezvousTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
        var stopwatch = Stopwatch.StartNew();
        File.WriteAllText(executionReleaseFile, string.Empty);
        TestHostResult testHostResult = await executionTask;

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.IsTrue(File.Exists(finalizationStartedFile), testHostResult.ToString());
        Assert.IsFalse(File.Exists(disposalFile), testHostResult.ToString());

        // Timing starts before releasing execution to trigger the 500ms test-host timeout. Five seconds gives the
        // combined test-host and finalization budgets ample CI margin while remaining below the 10-second blocker.
        Assert.IsLessThan(MaximumFinalizationTime, stopwatch.Elapsed, testHostResult.ToString());
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Timeout_BoundsBlockingDisposalWithoutRetryingIt(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string disposalAttemptsFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.dispose-attempts.txt");
        string executionReadyFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.ready.txt");
        string executionReleaseFile = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.release.txt");
        using var rendezvousTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        rendezvousTimeout.CancelAfter(RendezvousWaitTimeout);

        Task<TestHostResult> executionTask = testHost.ExecuteAsync(
            "--timeout 500ms",
            new()
            {
                ["BLOCK_UNTIL_TIMEOUT"] = "1",
                ["BLOCK_DISPOSAL"] = "1",
                ["DISPOSAL_ATTEMPTS_FILE"] = disposalAttemptsFile,
                ["EXECUTION_READY_FILE"] = executionReadyFile,
                ["EXECUTION_RELEASE_FILE"] = executionReleaseFile,
                ["TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS"] = "0.5",
                ["SKIP_FIXED_LIFECYCLE_FILES"] = "1",
            },
            cancellationToken: rendezvousTimeout.Token);

        await WaitForFileAsync(executionReadyFile, rendezvousTimeout.Token);
        rendezvousTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
        var stopwatch = Stopwatch.StartNew();
        File.WriteAllText(executionReleaseFile, string.Empty);
        TestHostResult testHostResult = await executionTask;

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.HasCount(1, File.ReadAllLines(disposalAttemptsFile), testHostResult.ToString());

        // Timing starts before releasing execution to trigger the 500ms test-host timeout. Five seconds gives the
        // combined test-host and finalization budgets ample CI margin while remaining below the 10-second blocker.
        Assert.IsLessThan(MaximumFinalizationTime, stopwatch.Elapsed, testHostResult.ToString());
    }

    private static async Task WaitForFileAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(10, cancellationToken);
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file TestHostProcessLifetimeHandler.csproj

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler());
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TestHostProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IDisposable
{
    public string Uid => nameof(TestHostProcessLifetimeHandler);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("SKIP_FIXED_LIFECYCLE_FILES") != "1")
        {
            System.IO.File.WriteAllText("BeforeTestHostProcessStartAsync.txt", "TestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync");
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("SKIP_FIXED_LIFECYCLE_FILES") != "1")
        {
            System.IO.File.WriteAllText("OnTestHostProcessExitedAsync.txt", "TestHostProcessLifetimeHandler.OnTestHostProcessExitedAsync");
        }

        if (Environment.GetEnvironmentVariable("FINALIZATION_FILE") is { Length: > 0 } finalizationFile)
        {
            System.IO.File.WriteAllText(finalizationFile, cancellationToken.IsCancellationRequested.ToString());
        }

        if (Environment.GetEnvironmentVariable("FINALIZATION_STARTED_FILE") is { Length: > 0 } finalizationStartedFile)
        {
            System.IO.File.WriteAllText(finalizationStartedFile, string.Empty);
        }

        if (Environment.GetEnvironmentVariable("BLOCK_FINALIZATION") == "1")
        {
            Thread.Sleep(10000);
        }

        return Task.CompletedTask;
    }

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("SKIP_FIXED_LIFECYCLE_FILES") != "1")
        {
            System.IO.File.WriteAllText("OnTestHostProcessStartedAsync.txt", "TestHostProcessLifetimeHandler.OnTestHostProcessStartedAsync");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("DISPOSAL_ATTEMPTS_FILE") is { Length: > 0 } disposalAttemptsFile)
        {
            System.IO.File.AppendAllText(disposalAttemptsFile, "Dispose" + Environment.NewLine);
        }

        if (Environment.GetEnvironmentVariable("DISPOSAL_FILE") is { Length: > 0 } disposalFile)
        {
            System.IO.File.WriteAllText(disposalFile, string.Empty);
        }

        if (Environment.GetEnvironmentVariable("BLOCK_DISPOSAL") == "1")
        {
            Thread.Sleep(10000);
        }
    }
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
        if (Environment.GetEnvironmentVariable("BLOCK_UNTIL_TIMEOUT") == "1")
        {
            if (Environment.GetEnvironmentVariable("EXECUTION_READY_FILE") is { Length: > 0 } readyFile)
            {
                System.IO.File.WriteAllText(readyFile, string.Empty);
            }

            if (Environment.GetEnvironmentVariable("EXECUTION_RELEASE_FILE") is { Length: > 0 } releaseFile)
            {
                while (!System.IO.File.Exists(releaseFile))
                {
                    Thread.Sleep(10);
                }
            }

            Thread.Sleep(2000);
        }

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

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
