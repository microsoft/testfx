// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class CrashPlusHangDumpTests : AcceptanceTestBase<CrashPlusHangDumpTests.TestAssetFixture>
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX, IgnoreMessage = "Investigate failures on macos")]
    public async Task CrashPlusHangDump_InCaseOfCrash_CreateCrashDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashPlusHangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 5m --crashdump --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                        { "SHOULDCRASH", "true" },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        testHostResult.AssertOutputMatchesRegex(@"Test host process with PID \'.+\' crashed, a dump file was generated");
        testHostResult.AssertOutputDoesNotContain(@"Hang dump timeout '00:00:08' expired");

        Assert.IsGreaterThan(0, Directory.GetFiles(resultDirectory, "CrashPlusHangDump*_crash.dmp", SearchOption.AllDirectories).Length, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
        Assert.IsLessThanOrEqualTo(0, Directory.GetFiles(resultDirectory, "CrashPlusHangDump*_hang.dmp", SearchOption.AllDirectories).Length, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX, IgnoreMessage = "Investigate failures on macos")]
    public async Task CrashPlusHangDump_InCaseOfHang_CreateHangDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashPlusHangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --crashdump --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                        { "SHOULDCRASH", "false" },
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        testHostResult.AssertOutputDoesNotMatchRegex(@"Test host process with PID '.+' crashed, a dump file was generated");
        testHostResult.AssertOutputContains(@"Hang dump timeout of '00:00:08' expired");

        Assert.IsLessThanOrEqualTo(0, Directory.GetFiles(resultDirectory, "CrashPlusHangDump.dll*_crash.dmp", SearchOption.AllDirectories).Length, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
        Assert.IsGreaterThan(0, Directory.GetFiles(resultDirectory, "CrashPlusHangDump*_hang.dmp", SearchOption.AllDirectories).Length, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "AssetFixture";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        private const string Sources = """
#file CrashPlusHangDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFrameworks$</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
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
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
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
        string shouldCrash = Environment.GetEnvironmentVariable("SHOULDCRASH")!;

        if (shouldCrash == "true")
        {
            Environment.FailFast("CrashPlusHangDump");
        }

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

    public TestContext TestContext { get; set; }
}
