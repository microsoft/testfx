// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class CrashDumpTests : AcceptanceTestBase<CrashDumpTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_DefaultSetting_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{tfm}'\n{testHostResult}'");
    }

    [TestMethod]
    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories), $"Dump file not found\n{testHostResult}");
    }

    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    [TestMethod]
    public async Task CrashDump_Formats_CreateDump(string format)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-type {format} --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string dumpFile = Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories), $"Dump file not found '{format}'\n{testHostResult}");
        File.Delete(dumpFile);
    }

    [TestMethod]
    public async Task CrashDump_WithCrashReport_CreateDumpAndCrashReport()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashreport --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories), $"Dump file not found\n{testHostResult}");
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp.crashreport.json", SearchOption.AllDirectories), $"Crash report file not found\n{testHostResult}");
    }

    [TestMethod]
    public async Task CrashReportOnly_CustomDumpName_CreateOnlyCrashReport()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashreport-only --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        // Use an explicit equality check on the file name rather than relying on Directory.GetFiles' pattern matching,
        // because on Windows a literal pattern such as "customdumpname.dmp" can also match files like
        // "customdumpname.dmp.crashreport.json" (8.3 short-name aliases / extension prefix matching).
        string[] dumpFiles = [.. Directory.EnumerateFiles(resultDirectory, "*", SearchOption.AllDirectories)
            .Where(f => string.Equals(Path.GetFileName(f), "customdumpname.dmp", StringComparison.Ordinal))];
        Assert.IsEmpty(dumpFiles, $"Unexpected dump file found\n{testHostResult}");
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "customdumpname.dmp.crashreport.json", SearchOption.AllDirectories), $"Crash report file not found\n{testHostResult}");
    }

    [TestMethod]
    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump  --crashdump-type invalid --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'");
    }

    [TestMethod]
    public async Task CrashReport_Without_CrashDump_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashreport --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("You specified '--crashreport' but did not enable crash dumps, add --crashdump to the command line");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetName = "CrashDumpFixture";

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        public string TargetAssetPath => GetAssetPath(AssetName);

        private const string Sources = """
#file CrashDump.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
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
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

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

    public TestContext TestContext { get; set; }
}
