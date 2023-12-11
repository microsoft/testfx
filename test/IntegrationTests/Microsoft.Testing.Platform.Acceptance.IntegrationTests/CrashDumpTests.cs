// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class CrashDumpTests : BaseAcceptanceTests
{
    internal static Func<Exception, bool> RetryPolicy
        => ex => ex.ToString().Contains("FAILED No such process")
        || ex.ToString().Contains("FAILED 13 (Permission denied)")
        || ex.ToString().Contains("Problem suspending threads");

    private readonly HangDumpFixture _hangDumpFixture;

    public CrashDumpTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, HangDumpFixture hangDumpFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _hangDumpFixture = hangDumpFixture;
    }

    [ArgumentsProvider(nameof(NET_Tfms))]
    public async Task CrashDump_DefaultSetting_CreateDump(string tfm)
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashDump", tfm);
                TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}");
                Assert.AreEqual(ExitCodes.TestHostProcessExitedNonGracefully, testHostResult.ExitCode, testHostResult.ToString());
                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump.dll_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), RetryPolicy);

    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashDump", MainNET_Tfm.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}");
        Assert.AreEqual(ExitCodes.TestHostProcessExitedNonGracefully, testHostResult.ExitCode, testHostResult.ToString());
        Assert.IsTrue(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories).SingleOrDefault() is not null, "Dump file not found");
    }

    [Arguments("Mini")]
    [Arguments("Heap")]
    [Arguments("Triage")]
    [Arguments("Full")]
    public async Task CrashDump_Formats_CreateDump(string format)
        => await RetryHelper.Retry(
            async () =>
            {
                string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
                TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashDump", MainNET_Tfm.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-type {format} --results-directory {resultDirectory}");
                Assert.AreEqual(ExitCodes.TestHostProcessExitedNonGracefully, testHostResult.ExitCode, $"{testHostResult}\n{format}");
                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump.dll_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{format}'\n{testHostResult}'");
                File.Delete(dumpFile);
            }, 3, TimeSpan.FromSeconds(3), RetryPolicy);

    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(_hangDumpFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_hangDumpFixture.TargetAssetPath, "CrashDump", MainNET_Tfm.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump  --crashdump-type invalid --results-directory {resultDirectory}");
        Assert.AreEqual(ExitCodes.InvalidCommandLine, testHostResult.ExitCode, testHostResult.ToString());
        Assert.That(testHostResult.StandardOutput.Contains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'", StringComparison.OrdinalIgnoreCase), testHostResult.StandardOutput);
    }

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
            _testAsset = await TestAsset.GenerateAssetAsync("CrashDumpFixture", Sources.PatchCodeWithRegularExpression("tfms", All_Tfms.ToTargetFrameworksElementContent()));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string Sources = """
#file CrashDump.csproj

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
    <PackageReference Include="Microsoft.Testing.Platform.Extensions" Version="[1.0.0-*,)" />
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
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        builder.AddCrashDumpGenerator();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestAdapter : ITestFramework
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Environment.FailFast("CrashDump");
        return Task.CompletedTask;
    }
}
""";
}
