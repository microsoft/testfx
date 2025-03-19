// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AbortionTests : AcceptanceTestBase<AbortionTests.TestAssetFixture>
{
    private const string AssetName = "Abort";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AbortWithCTRLPlusC_CancellingParallelTests(string tfm)
    {
        await AbortWithCTRLPlusC_CancellingTests(tfm, parallelize: true);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AbortWithCTRLPlusC_CancellingNonParallelTests(string tfm)
    {
        await AbortWithCTRLPlusC_CancellingTests(tfm, parallelize: false);
    }

    internal async Task AbortWithCTRLPlusC_CancellingTests(string tfm, bool parallelize)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        string? parameters = null;
        if (parallelize)
        {
            // Providing runSettings even with Parallelize Workers = 1, will "enable" parallelization and will run via different path.
            // So providing the settings only to the parallel run.
            string runSettingsPath = Path.Combine(testHost.DirectoryName, $"{(parallelize ? "parallel" : "serial")}.runsettings");
            File.WriteAllText(runSettingsPath, $"""
            <RunSettings>
                <MSTest>
                <Parallelize>
                    <Workers>{(parallelize ? 0 : 1)}</Workers>
                    <Scope>MethodLevel</Scope>
                </Parallelize>
                </MSTest>
            </RunSettings>
            """);
            parameters = $"--settings {runSettingsPath}";
        }

        string fileCreationPath = Path.Combine(testHost.DirectoryName, "fileCreation");

        TestHostResult testHostResult = await testHost.ExecuteAsync(parameters, environmentVariables: new()
        {
            ["FILE_DIRECTORY"] = fileCreationPath,
        });

        // To ensure we don't cancel right away, so tests have chance to run, and block our
        // cancellation if we do it wrong.
        testHostResult.AssertOutputContains("Waiting for file creation.");
        if (parallelize)
        {
            testHostResult.AssertOutputContains("Test Parallelization enabled for");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("Test Parallelization enabled for");
        }

        testHostResult.AssertExitCodeIs(ExitCodes.TestSessionAborted);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file Abort.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <GenerateProgramFile>false</GenerateProgramFile>
    <LangVersion>preview</LangVersion>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
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
using System.IO;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

        using ITestApplication app = await builder.BuildAsync();
        _ = Task.Run(() =>
        {
            while (true)
            {
                if (File.Exists(Environment.GetEnvironmentVariable("FILE_DIRECTORY")))
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Waiting for file creation.");
                    Thread.Sleep(1000);
                }
            }

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
        CTRL_C = 0
    }
}

#file UnitTest1.cs
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task TestA()
    {
        var fireCtrlCTask = Task.Run(() =>
        {
            // Delay for a short period before firing CTRL+C to simulate some processing time
            File.WriteAllText(Environment.GetEnvironmentVariable("FILE_DIRECTORY")!, string.Empty);
        });

        // Wait for 10s, and after that kill the process.
        // When we cancel by CRTL+C we do non-graceful teardown so the Environment.Exit should never be reached,
        // because the test process already terminated.
        //
        // If we do reach it, we will see 11111 exit code, and it will fail the test assertion, because we did not cancel.
        // (If we don't exit here, the process will happily run to completion after 10 seconds, but will still report
        // cancelled exit code, so that is why we are more aggressive here.)
        await Task.Delay(10_000);
        Environment.Exit(11111);
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
