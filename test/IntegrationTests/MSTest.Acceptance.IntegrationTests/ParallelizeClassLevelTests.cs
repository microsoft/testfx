// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ParallelizeClassLevelTests : AcceptanceTestBase<ParallelizeClassLevelTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ParallelizeClassLevel_WithoutAssemblyLevelParallelize_OptedInClassesRunInParallel(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "ClassLevelParallelizeAttributeProject";

        private const string SourceCode = """
#file ClassLevelParallelizeAttributeProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
namespace ClassLevelParallelizeAttributeProject;

using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class ParallelProbe
{
    private static readonly ManualResetEventSlim FirstEntered = new(initialState: false);
    private static readonly ManualResetEventSlim SecondEntered = new(initialState: false);
    private static readonly CountdownEvent ParallelTestsRemaining = new(initialCount: 2);
    private static int s_entered;
    private static int s_parallelPhaseCompleted;
    private static long s_firstParallelStartTimestamp;
    private static long s_parallelPhaseCompletedTimestamp;

    public static long FirstParallelStartTimestamp => Volatile.Read(ref s_firstParallelStartTimestamp);

    public static long ParallelPhaseCompletedTimestamp => Volatile.Read(ref s_parallelPhaseCompletedTimestamp);

    public static bool IsParallelPhaseCompleted => Volatile.Read(ref s_parallelPhaseCompleted) == 1;

    public static void EnterAndAssertParallel(string testName)
    {
        int entered = Interlocked.Increment(ref s_entered);

        if (entered == 1)
        {
            Interlocked.CompareExchange(ref s_firstParallelStartTimestamp, DateTimeOffset.UtcNow.Ticks, 0);
        }

        if (entered == 1)
        {
            FirstEntered.Set();

            if (!SecondEntered.Wait(15000))
            {
                Assert.Fail($"Expected class-level parallel execution, but no concurrent test entered while running '{testName}'.");
            }
        }
        else
        {
            SecondEntered.Set();
            FirstEntered.Wait(15000);
        }

        Interlocked.Decrement(ref s_entered);

        ParallelTestsRemaining.Signal();
        if (ParallelTestsRemaining.CurrentCount == 0)
        {
            Interlocked.Exchange(ref s_parallelPhaseCompletedTimestamp, DateTimeOffset.UtcNow.Ticks);
            Interlocked.Exchange(ref s_parallelPhaseCompleted, 1);
        }
    }
}

[TestClass]
[Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]
public class ParallelClass1
{
    [TestMethod]
    public void TestMethod1() => ParallelProbe.EnterAndAssertParallel(nameof(TestMethod1));
}

[TestClass]
[Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]
public class ParallelClass2
{
    [TestMethod]
    public void TestMethod1() => ParallelProbe.EnterAndAssertParallel(nameof(TestMethod1));
}

[TestClass]
public class SerialClass
{
    [TestMethod]
    public void TestMethod1()
    {
        long serialStartTimestamp = DateTimeOffset.UtcNow.Ticks;
        long firstParallelStartTimestamp = ParallelProbe.FirstParallelStartTimestamp;
        long parallelPhaseCompletedTimestamp = ParallelProbe.ParallelPhaseCompletedTimestamp;

        Assert.IsTrue(
            ParallelProbe.IsParallelPhaseCompleted,
            $"Serial test started before class-level parallelized tests completed. serialStartUtcTicks={serialStartTimestamp}; firstParallelStartUtcTicks={firstParallelStartTimestamp}; parallelPhaseCompletedUtcTicks={parallelPhaseCompletedTimestamp}");
    }
}
""";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
