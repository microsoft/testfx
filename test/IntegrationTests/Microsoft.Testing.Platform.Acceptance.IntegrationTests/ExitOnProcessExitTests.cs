// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ExitOnProcessExitTests : AcceptanceTestBase<ExitOnProcessExitTests.TestAssetFixture>
{
    private const string AssetName = "ExecutionTests";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task ExitOnProcessExit_Succeed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory state = new();

        // Named mutexes are not compatible across the runner's and test host's runtime versions on Unix.
        string readyFile = Path.Combine(state.Path, "ready");
        Dictionary<string, string?> environmentVariables = [];
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            string key = variable.Key.ToString()!;
            if (!WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                environmentVariables[key] = variable.Value?.ToString();
            }
        }

        environmentVariables["DOTNET_ROOT"] = Path.Combine(RootFinder.Find(), ".dotnet");
        environmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environmentVariables["TESTINGPLATFORM_AZDO_OUTPUT"] = "off";
        environmentVariables["ExitOnProcessExitReadyFile"] = readyFile;
        ConcurrentQueue<string> output = new();

        IProcessHandle parent = ProcessFactory.Start(
            new ProcessConfiguration(testHost.FullName)
            {
                Arguments = "--parent-process",
                EnvironmentVariables = environmentVariables,
                OnStandardOutput = (_, line) => output.Enqueue(line),
                OnErrorOutput = (_, line) => output.Enqueue(line),
            },
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true);

        // Keep the original exit waits uncanceled so failure cleanup can join both processes.
        Task<int> parentExit = parent.WaitForExitAsync(CancellationToken.None);
        IProcessHandle? child = null;
        Task<int>? childExit = null;
        try
        {
            child = ProcessFactory.Start(
                new ProcessConfiguration(testHost.FullName)
                {
                    Arguments = $"--exit-on-process-exit {parent.Id} --progress off --no-ansi",
                    EnvironmentVariables = environmentVariables,
                    OnStandardOutput = (_, line) => output.Enqueue(line),
                    OnErrorOutput = (_, line) => output.Enqueue(line),
                },
                cleanDefaultEnvironmentVariableIfCustomAreProvided: true);
            childExit = child.WaitForExitAsync(CancellationToken.None);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            startup.CancelAfter(StartupTimeout);
            while (!File.Exists(readyFile))
            {
                if (childExit.IsCompleted)
                {
                    Assert.Fail($"Test host exited before becoming ready with code {await childExit}.{Environment.NewLine}{string.Join(Environment.NewLine, output)}");
                }

                Assert.IsFalse(parentExit.IsCompleted, "The parent must remain alive until the test host is ready.");
                await Task.Delay(100, startup.Token);
            }

            await parent.WriteInputAsync("exit");
            Assert.AreEqual(0, await parentExit.WaitAsync(ProcessExitTimeout, TestContext.CancellationToken));
            Assert.AreEqual((int)ExitCode.DependentProcessExited, await childExit.WaitAsync(ProcessExitTimeout, TestContext.CancellationToken),
                string.Join(Environment.NewLine, output));
        }
        finally
        {
            parent.Kill();
            child?.Kill();
            try
            {
                Task<int>[] exits = childExit is null ? [parentExit] : [parentExit, childExit];
                await Task.WhenAll(exits).WaitAsync(CleanupTimeout, CancellationToken.None);
            }
            finally
            {
                child?.Dispose();
                parent.Dispose();
            }
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
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
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

if (args.Contains("--parent-process"))
{
    Console.ReadLine();
    return 0;
}

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

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
        File.WriteAllText(Environment.GetEnvironmentVariable("ExitOnProcessExitReadyFile")!, "ready");
        await Task.Delay(Timeout.Infinite, context.CancellationToken);
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

    public TestContext TestContext { get; set; }
}
