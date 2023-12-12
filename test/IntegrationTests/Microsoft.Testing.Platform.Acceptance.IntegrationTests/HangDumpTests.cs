// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class HangDumpTests : BaseAcceptanceTests
{
    private readonly HangDumpFixture _hangDumpFixture;

    public HangDumpTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, HangDumpFixture hangDumpFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _hangDumpFixture = hangDumpFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task HangDump_DefaultSetting_CreateDump(string tfm)
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "HangDump", tfm);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 8s --results-directory {resultDirectory}",
                    new Dictionary<string, string>()
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                    });
                testHostResult.AssertHasExitCode(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    public async Task HangDump_CustomFileName_CreateDump()
    {
        string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent.Arguments);
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename myhungdumpfile_%p.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string>()
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "myhungdumpfile_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsTrue(dumpFile is not null, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    [Arguments("Mini")]
    [Arguments("Heap")]
    [Arguments("Triage")]
    [Arguments("Full")]
    public async Task HangDump_Formats_CreateDump(string format)
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), format);
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 8s --hangdump-type {format} --results-directory {resultDirectory}",
                    new Dictionary<string, string>()
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                    });
                testHostResult.AssertHasExitCode(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{format}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    public async Task HangDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent.Arguments);
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-type invalid --results-directory {resultDirectory}",
            new Dictionary<string, string>()
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--hangdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' (only available in .NET 6+) and 'Full'");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class HangDumpFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _testAsset;

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public HangDumpFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync("HangDumpFixture", Sources.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToJoinedFrameworks()));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string Sources = """
#file HangDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>tfms</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.Extensions.HangDump" Version="[1.0.0-*,)" />
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
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        builder.AddHangDumpGenerator();
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty())
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS1")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test2",
            DisplayName = "Test2",
            Properties = new PropertyBag(new PassedTestNodeStateProperty())
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS2")!, CultureInfo.InvariantCulture));

        context.Complete();
    }
}
""";
}
