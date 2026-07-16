// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance-style rewrite of the former CLITestBase-based parallel execution tests. The parallel
/// test assets (method-level, class-level and do-not-parallelize) are generated inline, built, then
/// executed out-of-process through the MSTest runner (Microsoft.Testing.Platform) host.
/// </summary>
/// <remarks>
/// The original tests also asserted the wall-clock run time to prove parallelism. Those timing
/// assertions were already disabled upstream as flaky (they depend on the scheduling of the machine),
/// so this rewrite only pins the deterministic test outcomes.
/// </remarks>
[TestClass]
public sealed class ParallelExecutionTests : AcceptanceTestBase<ParallelExecutionTests.TestAssetFixture>
{
    private const string MethodParallelProjectName = "ParallelMethodsTestProject";
    private const string ClassParallelProjectName = "ParallelClassesTestProject";
    private const string DoNotParallelizeProjectName = "DoNotParallelizeTestProject";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task AllMethodsShouldRunInParallel()
    {
        TestHost testHost = AssetFixture.GetTestHost(MethodParallelProjectName);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 2", result.StandardOutput);
        Assert.Contains("succeeded: 4", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
    }

    [TestMethod]
    public async Task AllClassesShouldRunInParallel()
    {
        TestHost testHost = AssetFixture.GetTestHost(ClassParallelProjectName);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 3", result.StandardOutput);
        Assert.Contains("succeeded: 4", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
        Assert.Contains("SimpleTest32", result.StandardOutput);
    }

    [TestMethod]
    public async Task NothingShouldRunInParallel()
    {
        TestHost testHost = AssetFixture.GetTestHost(DoNotParallelizeProjectName);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 2", result.StandardOutput);
        Assert.Contains("succeeded: 3", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private readonly Dictionary<string, TestAsset> _assets = [];

        public TestHost GetTestHost(string projectName)
            => TestHost.LocateFrom(_assets[projectName].TargetAssetPath, projectName, "net462");

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            foreach ((string projectName, string code) in new[]
            {
                (MethodParallelProjectName, MethodParallelSourceCode),
                (ClassParallelProjectName, ClassParallelSourceCode),
                (DoNotParallelizeProjectName, DoNotParallelizeSourceCode),
            })
            {
                string patched = code.PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);
                TestAsset asset = await TestAsset.GenerateAssetAsync(projectName, patched, _tempDirectory);
                await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: projectName, cancellationToken: cancellationToken);
                _assets.Add(projectName, asset);
            }
        }

        public void Dispose()
        {
            foreach (TestAsset asset in _assets.Values)
            {
                asset.Dispose();
            }

            _tempDirectory.Dispose();
        }

        private const string ProjectFile = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>net462</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>
""";

        private static readonly string MethodParallelSourceCode = ProjectFile.Replace("$ProjectName$", MethodParallelProjectName) + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]

namespace ParallelMethodsTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest12()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    public void SimpleTest13()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }
}

[TestClass]
public class UnitTest2
{
    [TestMethod]
    public void SimpleTest21()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(0, 0);
    }

    [TestMethod]
    public void SimpleTest22()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    [DoNotParallelize]
    public void IsolatedTest()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.IsTrue(true);
    }
}
""";

        private static readonly string ClassParallelSourceCode = ProjectFile.Replace("$ProjectName$", ClassParallelProjectName) + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]

namespace ParallelClassesTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest12()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }
}

[TestClass]
public class UnitTest2
{
    [TestMethod]
    public void SimpleTest21()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(0, 0);
    }

    [TestMethod]
    public void SimpleTest22()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    [DoNotParallelize]
    public void IsolatedTest()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.IsTrue(true);
    }
}

[TestClass]
public class UnitTest3
{
    [TestMethod]
    public void SimpleTest31()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest32()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }
}
""";

        private static readonly string DoNotParallelizeSourceCode = ProjectFile.Replace("$ProjectName$", DoNotParallelizeProjectName) + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace DoNotParallelizeTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest12()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }

    [TestMethod]
    public void SimpleTest13()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }
}

[TestClass]
public class UnitTest2
{
    [TestMethod]
    public void SimpleTest21()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.AreEqual(1, 1);
    }

    [TestMethod]
    public void SimpleTest22()
    {
        Thread.Sleep(Constants.WaitTimeInMS);
        Assert.Fail();
    }
}
""";
    }
}
