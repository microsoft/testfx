// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class UnhandledExceptionPolicyTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public UnhandledExceptionPolicyTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    public enum Mode
    {
        Enabled,
        Disabled,
        DisabledByEnvironmentVariable,
        EnabledByEnvironmentVariable,
        Default,
    }

    internal static IEnumerable<TestArgumentsEntry<(Mode Mode, string Arguments)>> ModeProvider()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            yield return new TestArgumentsEntry<(Mode, string)>((Mode.Enabled, tfm.Arguments), $"Enabled - {tfm.Arguments}");
            yield return new TestArgumentsEntry<(Mode, string)>((Mode.Disabled, tfm.Arguments), $"Disabled - {tfm.Arguments}");
            yield return new TestArgumentsEntry<(Mode, string)>((Mode.DisabledByEnvironmentVariable, tfm.Arguments), $"DisabledByEnvironmentVariable - {tfm.Arguments}");
            yield return new TestArgumentsEntry<(Mode, string)>((Mode.EnabledByEnvironmentVariable, tfm.Arguments), $"EnabledByEnvironmentVariable - {tfm.Arguments}");
            yield return new TestArgumentsEntry<(Mode, string)>((Mode.Default, tfm.Arguments), $"Default - ({tfm.Arguments})");
        }
    }

    [ArgumentsProvider(nameof(ModeProvider))]
    public async Task UnhandledExceptionPolicy_ConfigFile_UnobservedTaskException_ShouldCrashProcessIfEnabled(Mode mode, string tfm)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "UnhandledExceptionPolicyTests", tfm);
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

                // We retry because we can have race issue on throwing unhandled exception and internal runtime handling
            }, 3, TimeSpan.FromSeconds(3));

    [ArgumentsProvider(nameof(ModeProvider))]
    public async Task UnhandledExceptionPolicy_EnvironmentVariable_UnhandledException_ShouldCrashProcessIfEnabled(Mode mode, string tfm)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, "UnhandledExceptionPolicyTests", tfm);
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

                // We retry because we can have race issue on throwing unhandled exception and internal runtime handling
            }, 3, TimeSpan.FromSeconds(3));

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
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
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
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
