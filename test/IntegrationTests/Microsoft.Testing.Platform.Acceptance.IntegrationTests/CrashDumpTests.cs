// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public sealed class CrashDumpTests : AcceptanceTestBase
{
    internal static Func<Exception, bool> RetryPolicy
        => ex => ex.ToString().Contains("FAILED No such process")
        || ex.ToString().Contains("FAILED 13 (Permission denied)")
        || ex.ToString().Contains("Problem suspending threads");

    private readonly TestAssetFixture _testAssetFixture;

    public CrashDumpTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task CrashDump_DefaultSetting_CreateDump(string tfm)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "CrashDump", tfm);
                TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}");
                testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump.dll_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{tfm}'\n{testHostResult}'");
            }, 3, TimeSpan.FromSeconds(3), RetryPolicy);

    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}");
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        Assert.IsTrue(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories).SingleOrDefault() is not null, "Dump file not found");
    }

    [Arguments("Mini")]
    [Arguments("Heap")]
    [Arguments("Triage")]
    [Arguments("Full")]
    public async Task CrashDump_Formats_CreateDump(string format)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent.Arguments);
                TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-type {format} --results-directory {resultDirectory}");
                testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
                string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump.dll_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
                Assert.IsTrue(dumpFile is not null, $"Dump file not found '{format}'\n{testHostResult}'");
                File.Delete(dumpFile);
            }, 3, TimeSpan.FromSeconds(3), RetryPolicy);

    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(_testAssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump  --crashdump-type invalid --results-directory {resultDirectory}");
        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "CrashDumpFixture";

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        public string TargetAssetPath => GetAssetPath(AssetName);

        private const string Sources = """
#file CrashDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
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
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        builder.AddCrashDumpProvider();
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Environment.FailFast("CrashDump");
        context.Complete();
        return Task.CompletedTask;
    }
}
""";
    }
}
