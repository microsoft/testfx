// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class HangDumpProcessTreeTests : AcceptanceTestBase<HangDumpProcessTreeTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HangDump_DumpAllChildProcesses_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"run --project {AssetFixture.TargetAssetPath} -f {tfm} --hangdump --hangdump-timeout 8s --hangdump-type mini --results-directory {resultDirectory}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            environmentVariables: new Dictionary<string, string?>
            {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
            },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories);
        Assert.HasCount(4, dumpFiles, $"There should be 4 dumps, one for each process in the tree. {result}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "AssetFixture";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        private const string Sources = """
#file HangDumpWithChild.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>

    <!-- Workaround: createdump doesn't work correctly on the apphost on macOS. -->
    <!-- But it works correctly on the dotnet process. -->
    <!-- So, disable apphost on macOS for now. -->
    <!-- Related: https://github.com/dotnet/runtime/issues/119945 -->
    <UseAppHost Condition="'$(OS)' == 'OSX'">false</UseAppHost>

    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        Process self = Process.GetCurrentProcess();
        string path = self.MainModule!.FileName!;

        if (args[0] == "--child")
        {
            int child = int.Parse(args[1], CultureInfo.InvariantCulture);

            if (child != 0)
            {
                var process = Process.Start(new ProcessStartInfo(path, $"--child {child - 1}")
                {
                    UseShellExecute = false,
                });

                // Wait for the child to exit, to make sure we dumping the process in order that will
                // end up with multiple dumps. Because typically the parent starts the child and waits for it.
                process!.WaitForExit();
                return 0;
            }
            else
            {        
                // Just sleep for a long time.
                Thread.Sleep(3_600_000);
                return 0;
            }
        }

        // We are running under testhost controller, don't start extra processes when we are the controller.
        if (args.Any(a => a == "--internal-testhostcontroller-pid"))
        {

            Process.Start(new ProcessStartInfo(path, $"--child 2")
            {
                UseShellExecute = false,
            });
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddHangDumpProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
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

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS1")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS2")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test2",
            DisplayName = "Test2",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        context.Complete();
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
