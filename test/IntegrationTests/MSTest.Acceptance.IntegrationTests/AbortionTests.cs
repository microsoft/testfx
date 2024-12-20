﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

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
    public async Task AbortWithCTRLPlusC_CancellingTests(string tfm)
    {
        // We expect the same semantic for Linux, the test setup is not cross and we're using specific
        // Windows API because this gesture is not easy xplat.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        string fileCreationPath = Path.Combine(testHost.DirectoryName, "fileCreation");
        File.WriteAllText(fileCreationPath, string.Empty);

        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["FILE_DIRECTORY"] = fileCreationPath,
        });

        testHostResult.AssertExitCodeIs(ExitCodes.TestSessionAborted);

        testHostResult.AssertOutputMatchesRegex("Canceling the test session.*");
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
            Task.Delay(1000).Wait();
            File.WriteAllText(Environment.GetEnvironmentVariable("FILE_DIRECTORY")!, string.Empty);
        });

        // Start a task that represents the infinite delay, which should be canceled
        await Task.Delay(Timeout.Infinite, TestContext.CancellationTokenSource.Token);
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
