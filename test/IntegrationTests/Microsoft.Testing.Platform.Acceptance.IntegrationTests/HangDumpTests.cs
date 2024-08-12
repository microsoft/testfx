// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class HangDumpTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public HangDumpTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task HangDump_DefaultSetting_CreateDump(string tfm)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "HangDump", tfm);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 8s --results-directory {resultDirectory}",
                    new Dictionary<string, string>
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                    });
                testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    public async Task HangDump_CustomFileName_CreateDump()
    {
        string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent.Arguments);
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename myhungdumpfile_%p.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            });
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "myhungdumpfile_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsTrue(dumpFile is not null, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    [Arguments("Mini")]
    [Arguments("Heap")]
    [Arguments("Triage")]
    [Arguments("Full")]
    public async Task HangDump_Formats_CreateDump(string format)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), format);
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 8s --hangdump-type {format} --results-directory {resultDirectory}",
                    new Dictionary<string, string>
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                    });
                testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{format}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    public async Task HangDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent.Arguments);
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-type invalid --results-directory {resultDirectory}",
            new Dictionary<string, string>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            });
        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--hangdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' (only available in .NET 6+) and 'Full'");
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
#file HangDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
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
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        builder.AddHangDumpProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS1")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test2",
            DisplayName = "Test2",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS2")!, CultureInfo.InvariantCulture));

        context.Complete();
    }
}
""";
    }
}
