// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class UnhandledExceptionPolicyTests : AcceptanceTestBase<UnhandledExceptionPolicyTests.TestAssetFixture>
{
    public enum Mode
    {
        Enabled,
        Disabled,
        DisabledByEnvironmentVariable,
        EnabledByEnvironmentVariable,
        Default,
    }

    internal static IEnumerable<(Mode Mode, string Arguments)> ModeProvider()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            yield return new(Mode.Enabled, tfm);
            yield return new(Mode.Disabled, tfm);
            yield return new(Mode.DisabledByEnvironmentVariable, tfm);
            yield return new(Mode.EnabledByEnvironmentVariable, tfm);
            yield return new(Mode.Default, tfm);
        }
    }

    [DynamicData(nameof(ModeProvider))]
    [TestMethod]
    public async Task UnhandledExceptionPolicy_ConfigFile_UnobservedTaskException_ShouldCrashProcessIfEnabled(Mode mode, string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "UnhandledExceptionPolicyTests", tfm);
        using TempDirectory clone = new();
        await clone.CopyDirectoryAsync(testHost.DirectoryName, clone.Path, retainAttributes: !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        testHost = TestInfrastructure.TestHost.LocateFrom(clone.Path, "UnhandledExceptionPolicyTests");
        string configFileName = Path.Combine(testHost.DirectoryName, "UnhandledExceptionPolicyTests.testconfig.json");
        string contentFile = await File.ReadAllTextAsync(Path.Combine(testHost.DirectoryName, "UnhandledExceptionPolicyTests.testconfig.json"));

        TestHostResult? testHostResult;
        switch (mode)
        {
            case Mode.Enabled:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": true"));
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNOBSERVEDTASKEXCEPTION", "1" } });
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                testHostResult.AssertOutputContains("[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException(testhost controller workflow)]");
                break;
            case Mode.Disabled:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": false"));
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNOBSERVEDTASKEXCEPTION", "1" } });
                testHostResult.AssertExitCodeIs(ExitCodes.Success);
                testHostResult.AssertOutputDoesNotContain("[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException]");
                break;
            case Mode.Default:
                File.Delete(configFileName);
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNOBSERVEDTASKEXCEPTION", "1" } });
                testHostResult.AssertExitCodeIs(ExitCodes.Success);
                testHostResult.AssertOutputDoesNotContain("[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException]");
                break;
            case Mode.DisabledByEnvironmentVariable:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": true"));
                testHostResult = await testHost.ExecuteAsync(null, new()
                        {
                            { "UNOBSERVEDTASKEXCEPTION", "1" },
                            { EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION, "0" },
                        });
                testHostResult.AssertExitCodeIs(ExitCodes.Success);
                testHostResult.AssertOutputDoesNotContain("[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException]");
                break;
            case Mode.EnabledByEnvironmentVariable:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": false"));
                testHostResult = await testHost.ExecuteAsync(null, new()
                        {
                            { "UNOBSERVEDTASKEXCEPTION", "1" },
                            { EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION, "1" },
                        });
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                testHostResult.AssertOutputContains("[UnhandledExceptionHandler.OnTaskSchedulerUnobservedTaskException(testhost controller workflow)]");
                break;
            default:
                throw new NotImplementedException($"Mode not found '{mode}'");
        }
    }

    [DynamicData(nameof(ModeProvider))]
    [TestMethod]
    public async Task UnhandledExceptionPolicy_EnvironmentVariable_UnhandledException_ShouldCrashProcessIfEnabled(Mode mode, string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "UnhandledExceptionPolicyTests", tfm);
        using TempDirectory clone = new();
        await clone.CopyDirectoryAsync(testHost.DirectoryName, clone.Path, retainAttributes: !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        testHost = TestInfrastructure.TestHost.LocateFrom(clone.Path, "UnhandledExceptionPolicyTests");
        string configFileName = Path.Combine(testHost.DirectoryName, "UnhandledExceptionPolicyTests.testconfig.json");
        string contentFile = await File.ReadAllTextAsync(Path.Combine(testHost.DirectoryName, "UnhandledExceptionPolicyTests.testconfig.json"));

        TestHostResult? testHostResult;
        switch (mode)
        {
            case Mode.Enabled:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": true"));
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNHANDLEDEXCEPTION", "1" } });
                testHostResult.AssertOutputContains("[UnhandledExceptionHandler.OnCurrentDomainUnhandledException(testhost controller workflow)]");
                testHostResult.AssertOutputContains("IsTerminating: True");
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                break;
            case Mode.Disabled:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": false"));
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNHANDLEDEXCEPTION", "1" } });
                Assert.IsTrue(testHostResult.StandardError.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase), testHostResult.ToString());
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                break;
            case Mode.Default:
                File.Delete(configFileName);
                testHostResult = await testHost.ExecuteAsync(null, new() { { "UNHANDLEDEXCEPTION", "1" } });
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                break;
            case Mode.DisabledByEnvironmentVariable:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": true"));
                testHostResult = await testHost.ExecuteAsync(null, new()
                         {
                             { "UNHANDLEDEXCEPTION", "1" },
                             { EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION, "0" },
                         });
                Assert.IsTrue(testHostResult.StandardError.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase), testHostResult.ToString());
                testHostResult.AssertOutputDoesNotContain("IsTerminating: True");
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                break;
            case Mode.EnabledByEnvironmentVariable:
                File.WriteAllText(configFileName, contentFile.Replace("\"exitProcessOnUnhandledException\": false", "\"exitProcessOnUnhandledException\": false"));
                testHostResult = await testHost.ExecuteAsync(null, new()
                        {
                            { "UNHANDLEDEXCEPTION", "1" },
                            { EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION, "1" },
                        });
                testHostResult.AssertOutputContains("[UnhandledExceptionHandler.OnCurrentDomainUnhandledException(testhost controller workflow)]");
                testHostResult.AssertOutputContains("IsTerminating: True");
                testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
                break;
            default:
                throw new NotImplementedException($"Mode not found '{mode}'");
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string AssetName = "UnhandledExceptionPolicyTests";

        private const string Sources = """
#file UnhandledExceptionPolicyTests.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

  <ItemGroup>
   <None Update="*.testconfig.json">
     <CopyToOutputDirectory>Always</CopyToOutputDirectory>
   </None>
  </ItemGroup>
</Project>

#file UnhandledExceptionPolicyTests.testconfig.json

{
  "platformOptions": {
    "telemetry": {
      "isDevelopmentRepository": true
    },
    "exitProcessOnUnhandledException": false
  }
}


#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var message = new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
        });
        message.Properties.Add(new PassedTestNodeStateProperty());
        await context.MessageBus.PublishAsync(this, message);

        if ( Environment.GetEnvironmentVariable("UNOBSERVEDTASKEXCEPTION") == "1")
        {
            var task = Task.Run(() => throw new Exception("Unhandled Exception"));
            while(!task.IsCompleted);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            task = Task.Run(() => throw new Exception("Unhandled Exception"));
            while(!task.IsCompleted);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            task = Task.Run(() => throw new Exception("Unhandled Exception"));
            while(!task.IsCompleted);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if ( Environment.GetEnvironmentVariable("UNHANDLEDEXCEPTION") == "1")
        {
            var t = new System.Threading.Thread(
            new ThreadStart(() =>
            {
               throw new Exception("Unhandled Exception");
            }));
            t.Start();
            t.Join();
        }

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }
}
