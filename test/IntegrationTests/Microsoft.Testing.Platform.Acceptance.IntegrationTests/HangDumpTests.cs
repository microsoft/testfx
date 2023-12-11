// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class HangDumpTests : BaseAcceptanceTests
{
    private readonly HangDumpFixture _hangDumpFixture;

    public HangDumpTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, HangDumpFixture nangDumpFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _hangDumpFixture = nangDumpFixture;
    }

    public async Task HangDump_InCaseOfCrash_CreateCrashDump()
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), MainNET_Tfm.Arguments);
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashPlusHangDump", MainNET_Tfm.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 5m --crashdump --results-directory {resultDirectory}",
                    new Dictionary<string, string>()
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                        { "SHOULDCRASH", "true" },
                    });
                Assert.AreEqual(ExitCodes.TestHostProcessExitedNonGracefully, testHostResult.ExitCode, testHostResult.ToString());

                Assert.That(Regex.IsMatch(testHostResult.StandardOutput, @"Test host process with PID \'.+\' crashed, a dump file was generated"), testHostResult.StandardOutput);
                Assert.IsFalse(Regex.IsMatch(testHostResult.StandardOutput, @"Hang dump timeout '00:00:08' expired"), testHostResult.StandardOutput);

                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashPlusHangDump.dll*_crash.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{MainNET_Tfm}'\n{testHostResult}'");
                dumpFile = Directory.GetFiles(resultDirectory, "CrashPlusHangDump*_hang.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsFalse(dumpFile is not null, $"Dump file not found '{MainNET_Tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    public async Task HangDump_InCaseOfHang_CreateHangDump()
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), MainNET_Tfm.Arguments);
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashPlusHangDump", MainNET_Tfm.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--hangdump --hangdump-timeout 8s --crashdump --results-directory {resultDirectory}",
                    new Dictionary<string, string>()
                    {
                        { "SLEEPTIMEMS1", "4000" },
                        { "SLEEPTIMEMS2", "600000" },
                        { "SHOULDCRASH", "false" },
                    });
                Assert.AreEqual(ExitCodes.TestHostProcessExitedNonGracefully, testHostResult.ExitCode, testHostResult.ToString());

                Assert.That(!Regex.IsMatch(testHostResult.StandardOutput, @"Test host process with PID '.+' crashed, a dump file was generated"), testHostResult.StandardOutput);
                Assert.That(Regex.IsMatch(testHostResult.StandardOutput, @"Hang dump timeout of '00:00:08' expired"), testHostResult.StandardOutput);

                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashPlusHangDump.dll*_crash.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsFalse(dumpFile is not null, $"Dump file not found '{MainNET_Tfm}'\n{testHostResult}'");
                dumpFile = Directory.GetFiles(resultDirectory, "CrashPlusHangDump*_hang.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{MainNET_Tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), CrashDumpTests.RetryPolicy);

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class HangDumpFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _testAsset;

        public HangDumpFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync("HangDumpFixture", Sources.PatchCodeWithRegularExpression("tfm", MainNET_Tfm.Arguments));
            await DotnetCli.RunAsync($"build {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string Sources = """
#file CrashPlusHangDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>tfm</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
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
        builder.AddCrashDumpGenerator();
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
        string shouldCrash = Environment.GetEnvironmentVariable("SHOULDCRASH")!;

        if (shouldCrash == "true")
        {
            Environment.FailFast("CrashPlusHangDump");
        }

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
