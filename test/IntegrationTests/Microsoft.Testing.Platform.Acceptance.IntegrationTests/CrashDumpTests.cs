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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{tfm}'\n{testHostResult}'");
    }

    [TestMethod]
    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        Assert.IsNotNull(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories).SingleOrDefault(), "Dump file not found");
    }

    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    [TestMethod]
    public async Task CrashDump_Formats_CreateDump(string format)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-type {format} --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{format}'\n{testHostResult}'");
        File.Delete(dumpFile);
    }

    [TestMethod]
    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump  --crashdump-type invalid --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'");
    }

    [TestMethod]
    public async Task CrashDump_WithChildProcess_CollectsMultipleDumps()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDumpWithChild", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);

        string[] dumpFiles = Directory.GetFiles(resultDirectory, "*.dmp", SearchOption.AllDirectories);
        Assert.IsGreaterThanOrEqualTo(2, dumpFiles.Length, $"Expected at least 2 dump files (parent and child), but found {dumpFiles.Length}. Dumps: {string.Join(", ", dumpFiles.Select(Path.GetFileName))}\n{testHostResult}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "CrashDumpFixture";

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

            yield return ("CrashDumpWithChildFixture", "CrashDumpWithChildFixture",
                SourcesWithChild
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

        private const string SourcesWithChild = """
#file CrashDumpWithChild.csproj
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
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        Process self = Process.GetCurrentProcess();
        string path = self.MainModule!.FileName!;

        // Handle child process execution
        if (args.Length > 0 && args[0] == "--child")
        {
            // Child process crashes immediately
            Environment.FailFast("Child process crash");
            return 1;
        }

        // Start a child process that will also crash (only when running as testhost controller)
        if (args.Any(a => a == "--internal-testhostcontroller-pid"))
        {
            try
            {
                var childProcess = Process.Start(new ProcessStartInfo(path, "--child")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                
                if (childProcess != null)
                {
                    // Give child process time to start and crash
                    Thread.Sleep(500);
                }
            }
            catch
            {
                // Ignore any errors starting child process
            }
        }

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
        // Parent process crashes
        Environment.FailFast("Parent process crash");
        context.Complete();
        return Task.CompletedTask;
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
