// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class AbortionTests : AcceptanceTestBase
{
    private const string AssetName = "Abort";
    private readonly TestAssetFixture _testAssetFixture;

    public AbortionTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AbortWithCTRLPlusC_CancellingTests(string tfm)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.TestSessionAborted);

        // We check only in netcore for netfx is now showing in CI every time, the same behavior in local something works sometime nope.
        // Manual test works pretty always as expected, looks like the implementation is different, we care more on .NET Core.
        if (TargetFrameworks.Net.Select(x => x.Arguments).Contains(tfm))
        {
            testHostResult.AssertOutputMatchesRegex("Canceling the test session.*");
        }

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 0, aborted: true);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file Abort.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <GenerateProgramFile>false</GenerateProgramFile>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

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

internal class DummyAdapter
{
    public static readonly ManualResetEventSlim FireCancel = new ManualResetEventSlim(false);

    public async Task ExecuteRequestAsync(CancellationToken cancellationToken)
    {
        // This will trigger pressing CTRL+C that should propagate through the platform
        // and down to us as the context.Cancellation token being canceled.
        // It should happen almost immediately, but we allow 15 seconds for this to happen
        // if it does not happen then the platform does not handle cancellation correctly and
        // the test fails.
        // If it happens, we return a result, and platform should report Aborted exit code and result.
        FireCancel.Set();

        var timeoutTask = Task.Delay(15_000, cancellationToken);
        await timeoutTask;
        if (!timeoutTask.IsCanceled)
        {
            throw new Exception("Cancellation was not propagated to the adapter within 15 seconds since CTRL+C.");
        }
    }
}

#file UnitTest1.cs
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void TestA()
    {
	    //ManualResetEventSlim waitHandle = new(false);
	    //waitHandle.Wait(TestContext.CancellationTokenSource.Token);
    }
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
