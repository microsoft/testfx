// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class AbortionTests : AcceptanceTestBase<AbortionTests.TestAssetFixture>
{
    private const string AssetName = "Abort";

    // We retry because sometime the Canceling the session message is not showing up.
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task AbortWithCTRLPlusC_TestHost_Succeeded(string tfm)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.TestSessionAborted);

        // We don't assert "Canceling the test session" message.
        // Cancellation could happen very first that we didn't have the opportunity to write this message.
        // However, the summary should always be correct and should always indicate that the session was aborted.
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 0, aborted: true);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file Abort.csproj
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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using System.Runtime.InteropServices;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        _ = Task.Run(() =>
        {
            DummyTestFramework.FireCancel.Wait();

            if (!GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0))
            {
                throw new Exception($"GetLastWin32Error '{Marshal.GetLastWin32Error()}'");
            }
        });
        return await app.RunAsync();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
    public enum ConsoleCtrlEvent
    {
        CTRL_C = 0,
        CTRL_BREAK = 1,
        CTRL_CLOSE = 2,
        CTRL_LOGOFF = 5,
        CTRL_SHUTDOWN = 6
    }

}

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public static readonly ManualResetEventSlim FireCancel = new ManualResetEventSlim(false);
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // This will trigger pressing CTRL+C that should propagate through the platform
        // and down to us as the context.Cancellation token being canceled.
        // It should happen almost immediately, but we allow 15 seconds for this to happen
        // if it does not happen then the platform does not handle cancellation correctly and
        // the test fails.
        // If it happens, we return a result, and platform should report Aborted exit code and result.
        FireCancel.Set();

        var timeoutTask = Task.Delay(15_000, context.CancellationToken);
        await timeoutTask;
        if (!timeoutTask.IsCanceled)
        {
            throw new Exception("Cancellation was not propagated to the adapter within 15 seconds since CTRL+C.");
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            // We expect the same semantic for Linux, the test setup is not cross and we're using specific
            // Windows API because this gesture is not easy xplat.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield break;
            }

            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
