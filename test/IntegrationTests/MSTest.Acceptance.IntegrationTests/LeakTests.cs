// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LeakTests : AcceptanceTestBase<LeakTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestContextInstancesShouldNotLeak(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 100, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "LeakTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file LeakTests.csproj
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;


[TestClass]
public class TestClass
{
    private static ConcurrentBag<WeakReference<TestContext>> _testContexts = new();

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
        => _testContexts.Add(new WeakReference<TestContext>(context));

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
        => _testContexts.Add(new WeakReference<TestContext>(context));

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void Test3(int a)
        => _testContexts.Add(new WeakReference<TestContext>(TestContext));

    [ClassCleanup]
    public static void ClassCleanup(TestContext testContext)
        => _testContexts.Add(new WeakReference<TestContext>(testContext));

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assembly init, class init, 100 tests, and class cleanup. (total 103).
        Assert.AreEqual(103, _testContexts.Count);

        var alive = 0;
        foreach (var weakReference in _testContexts)
        {
            if (weakReference.TryGetTarget(out _))
            {
                alive++;
            }
        }

        // AssemblyCleanup is executed along with the last test.
        // So, we are still holding 2 references to the TestContext. The one for the execution of last test, as well as the one for ClassCleanup.
        // Holding into these two references is okay.
        Assert.AreEqual(2, alive);
    }

    public static IEnumerable<int> Data
    {
        get
        {
            for (int i = 0; i < 100; i++)
            {
                yield return i;
            }
        }
    }
}

""";
    }

    public TestContext TestContext { get; set; }
}
