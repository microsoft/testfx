// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestConfigEnvironmentVariablesTests : AcceptanceTestBase<TestConfigEnvironmentVariablesTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task EnvironmentVariablesSection_DeclaredVariables_AreVisibleToTestHostProcess(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory clone = new();
        testHost = await CloneTestHostAsync(testHost, clone, AssetName);

        string configFile = Path.Combine(testHost.DirectoryName, $"{AssetName}.testconfig.json");
        await File.WriteAllTextAsync(
            configFile,
            """
            {
              "environmentVariables": {
                "TEST_CONFIG_VAR_FROM_FILE": "from-testconfig",
                "TEST_CONFIG_VAR_OTHER": "second-value"
              }
            }
            """,
            TestContext.CancellationToken);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("TEST_CONFIG_VAR_FROM_FILE=from-testconfig");
        testHostResult.AssertOutputContains("TEST_CONFIG_VAR_OTHER=second-value");
        // When the environmentVariables section is non-empty, the test host must be relaunched
        // under the test host controller process model so the variables can be applied to the
        // child process via ProcessStartInfo. Assert the controller-injected env var is present.
        testHostResult.AssertOutputDoesNotContain("TESTHOSTCONTROLLER_PARENTPID=<none>");
        testHostResult.AssertOutputMatchesRegex(@"TESTHOSTCONTROLLER_PARENTPID=\d+");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task EnvironmentVariablesSection_Absent_TestHostRunsInProcess(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory clone = new();
        testHost = await CloneTestHostAsync(testHost, clone, AssetName);

        string configFile = Path.Combine(testHost.DirectoryName, $"{AssetName}.testconfig.json");
        File.Delete(configFile);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        // Variable is not set, so the framework prints the sentinel.
        testHostResult.AssertOutputContains("TEST_CONFIG_VAR_FROM_FILE=<unset>");
        // When the environmentVariables section is absent, the provider must report itself as
        // disabled so the test host controller process model is not engaged and the test host
        // runs in the current process. Assert the controller-injected env var is *not* set.
        testHostResult.AssertOutputContains("TESTHOSTCONTROLLER_PARENTPID=<none>");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task EnvironmentVariablesSection_InvalidShape_FailsWithClearError(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory clone = new();
        testHost = await CloneTestHostAsync(testHost, clone, AssetName);

        string configFile = Path.Combine(testHost.DirectoryName, $"{AssetName}.testconfig.json");
        await File.WriteAllTextAsync(
            configFile,
            """
            {
              "environmentVariables": {
                "FOO": { "nested": "x" }
              }
            }
            """,
            TestContext.CancellationToken);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIsNot(ExitCode.Success);
        Assert.IsTrue(
            testHostResult.StandardOutput.Contains("environmentVariables", StringComparison.Ordinal)
                || testHostResult.StandardError.Contains("environmentVariables", StringComparison.Ordinal),
            $"Expected the failure output to mention the offending section. Output was:\n{testHostResult}");
    }

    internal const string AssetName = "TestConfigEnvironmentVariablesTests";

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file TestConfigEnvironmentVariablesTests.csproj

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

#file TestConfigEnvironmentVariablesTests.testconfig.json

{
  "environmentVariables": {
    "TEST_CONFIG_VAR_FROM_FILE": "from-testconfig",
    "TEST_CONFIG_VAR_OTHER": "second-value"
  }
}

#file Program.cs

using System;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string controllerParentPid = "<none>";
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--internal-testhostcontroller-pid")
            {
                controllerParentPid = args[i + 1];
                break;
            }
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework(controllerParentPid));
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public sealed class DummyTestFramework(string controllerParentPid) : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        foreach (string name in new[] { "TEST_CONFIG_VAR_FROM_FILE", "TEST_CONFIG_VAR_OTHER" })
        {
            string? value = Environment.GetEnvironmentVariable(name);
            // Use a stable sentinel so acceptance tests can assert both presence and absence.
            Console.WriteLine($"{name}={value ?? "<unset>"}");
        }

        // Surface whether the test host was relaunched under the test host controller process
        // model. The controller adds the hidden --internal-testhostcontroller-pid option only to
        // the spawned child process, which avoids false positives from inherited environment
        // variables when the outer acceptance test process itself runs under a controller.
        Console.WriteLine($"TESTHOSTCONTROLLER_PARENTPID={controllerParentPid}");

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
