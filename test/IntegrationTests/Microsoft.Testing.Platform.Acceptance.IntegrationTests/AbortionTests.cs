// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class AbortionTests : BaseAcceptanceTests
{
    private const string AssetName = "Abort";
    private readonly AbortionTestsFixture _abortionTestsFixture;

    public AbortionTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, AbortionTestsFixture abortionTestsFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _abortionTestsFixture = abortionTestsFixture;
    }

    [ArgumentsProvider(nameof(All_Tfms))]
    public async Task AbortWithCTRLPlusC_TestHost_Succeeded(string tfm)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_abortionTestsFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        Assert.AreEqual(ExitCodes.TestSessionAborted, testHostResult.ExitCode, because: testHostResult.ToString());

        // We check only in netcore for netfx is now showing in CI every time, the same behavior in local something works sometime nope.
        // Manual test works pretty always as expected, looks like the implementation is different, we care more on .NET Core.
        if (NET_Tfms.Select(x => x.Arguments).Contains(tfm))
        {
            Assert.That(Regex.IsMatch(testHostResult.StandardOutput, "Cancelling the test session.*"), testHostResult.ToString());
        }

        Assert.That(Regex.IsMatch(testHostResult.StandardOutput, "Aborted - Failed: 0, Passed: 0, Skipped: 0, Total: 0 -.*"), testHostResult.ToString());
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class AbortionTestsFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _testAsset;

        public AbortionTestsFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public string TargetAssetPath => _testAsset is null ? throw new ArgumentNullException(nameof(TestAsset)) : _testAsset.TargetAssetPath;

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync(AssetName, Sources.PatchCodeWithRegularExpression("tfms", All_Tfms.ToTargetFrameworksElementContent()));

            // We expect the same semantic for Linux, the test setup is not cross and we're using specific
            // Windows API because this gesture is not easy xplat.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string Sources = """
#file Abort.csproj
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
  </ItemGroup>
</Project>

#file Program.cs
using System;
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
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyAdapter());
        using ITestApplication app = await builder.BuildAsync();
        _ = Task.Run(() =>
        {
            DummyAdapter.FireCancel.Wait();

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

internal class DummyAdapter : ITestFramework, IDataProducer
{
    public static readonly ManualResetEventSlim FireCancel = new ManualResetEventSlim(false);
    public string Uid => nameof(DummyAdapter);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // This will trigger pressing CTRL+C that should propagate through the platform
        // and down to us as the context.Cancellation token being cancelled.
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
    ITestFrameworkCapability[] ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}

""";
}
