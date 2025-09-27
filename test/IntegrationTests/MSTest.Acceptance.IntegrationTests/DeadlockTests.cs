// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DeadlockTests : AcceptanceTestBase<DeadlockTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DeadlockCaseClassCleanupWaitingOnTestMethod(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter FullyQualifiedName~DeadlockCase1", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DeadlockCaseClassInitWaitingOnPreviousTestMethod(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter FullyQualifiedName~DeadlockCase2", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "DeadlockTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file DeadlockTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DeadlockCase1
{
    // This repro is intended to deadlock when Test1 is called asynchronously like await Test1(), but then ClassCleanup is called with GetAwaiter().GetResult().
    // When await Test1() is called, we return a Task that will complete in ~1second on a custom thread we created.
    // After that Task completes, the continuation will run on that exact same custom thread.
    // If the continuation calls ClassCleanup().GetAwaiter().GetResult(), then we are blocking that custom thread waiting for ClassCleanup to complete.
    // But ClassCleanup cannot complete until that custom thread calls _cts.SetResult(), which it cannot do because it is blocked waiting for ClassCleanup to complete.
    // So we deadlock.
    // This repro is related to https://github.com/microsoft/testfx/issues/6575
    private static TaskCompletionSource<object> _cts = new();

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
    {
        await _cts.Task;
    }

    [TestMethod]
    public async Task Test1()
    {
        var cts1 = new TaskCompletionSource<object>();
        var t = new Thread(() =>
        {
            Thread.Sleep(1000);
            cts1.SetResult(null);
            Thread.Sleep(1000);
            _cts.SetResult(null);
        });
        t.Start();
        await cts1.Task;
    }
}

[TestClass]
public class DeadlockCase2
{
    // This repro is intended to deadlock when Test1 is called asynchronously like await Test1(), but then TestInit (the invocation before Test2 and after Test1) is called with GetAwaiter().GetResult().
    // When await Test1() is called, we return a Task that will complete in ~1second on a custom thread we created.
    // After that Task completes, the continuation will run on that exact same custom thread.
    // If the continuation calls TestInit().GetAwaiter().GetResult(), then we are blocking that custom thread waiting for TestInit to complete.
    // But TestInit cannot complete until that custom thread calls _cts.SetResult(), which it cannot do because it is blocked waiting for TestInit to complete.
    // So we deadlock.
    // This repro is related to https://github.com/microsoft/testfx/issues/6575
    private static TaskCompletionSource<object> _cts = new();

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public async Task TestInit()
    {
        if (TestContext.TestName == nameof(Test2))
        {
            await _cts.Task;
        }
    }

    [TestMethod]
    public async Task Test1()
    {
        var cts1 = new TaskCompletionSource<object>();
        var t = new Thread(() =>
        {
            Thread.Sleep(1000);
            cts1.SetResult(null);
            Thread.Sleep(1000);
            _cts.SetResult(null);
        });
        t.Start();
        await cts1.Task;
    }

    [TestMethod]
    public void Test2()
    {
    }
}

""";
    }

    public TestContext TestContext { get; set; }
}
