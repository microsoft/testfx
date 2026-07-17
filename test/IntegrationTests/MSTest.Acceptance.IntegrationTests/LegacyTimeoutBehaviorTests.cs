// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

// Legacy equivalence map for MSTest.VstestConsoleWrapper.IntegrationTests.TimeoutTests.ValidateTimeoutTests:
// - TimeoutTest_WhenTimeoutReached_ForcesTestAbort is already equivalent to
//   MSTest.Acceptance.IntegrationTests.TimeoutTestMethodTests.Timeout_WhenMethodTimeoutAndWaitInTestMethod_TestGetsCanceled.
// - TimeoutTest_WhenTimeoutReached_CancelsTestContextToken had no marker/output equivalent and is preserved below.
// - TimeoutTest_WhenUserCancelsTestContextToken_AbortTest and
//   RegularTest_WhenUserCancelsTestContextToken_TestContinues had no acceptance test for their distinction and are preserved below.
// - TimeoutTest_WhenUserCallsThreadAbort_AbortTest had no acceptance equivalent and is preserved below for .NET Framework.
[TestClass]
public sealed class LegacyTimeoutBehaviorTests : AcceptanceTestBase<LegacyTimeoutBehaviorTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TimeoutExpiration_CancelsTestContextTokenAndAllowsBackgroundOutput(string tfm)
    {
        string markerPath = Path.Combine(Path.GetTempPath(), $"mstest-timeout-{Guid.NewGuid():N}.txt");
        try
        {
            TestHostResult result = await ExecuteAsync(
                tfm,
                "TimeoutExpirationCancelsToken",
                new() { ["TIMEOUT_MARKER"] = markerPath });

            result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            result.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
            result.AssertOutputContains("TimeoutExpirationCancelsToken");
            Assert.IsTrue(File.Exists(markerPath), $"Timeout cancellation marker was not created: {markerPath}");
            Assert.AreEqual("cancellation observed", File.ReadAllText(markerPath));
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task UserCancellation_WithAndWithoutTimeoutPreservesExactFailedTests(string tfm)
    {
        string timedMarkerPath = Path.Combine(Path.GetTempPath(), $"mstest-timed-cancel-{Guid.NewGuid():N}.txt");
        string regularMarkerPath = Path.Combine(Path.GetTempPath(), $"mstest-regular-cancel-{Guid.NewGuid():N}.txt");
        try
        {
            TestHostResult timedResult = await ExecuteAsync(
                tfm,
                "UserCancellationWithTimeout",
                new() { ["TIMED_CANCEL_MARKER"] = timedMarkerPath });
            TestHostResult regularResult = await ExecuteAsync(
                tfm,
                "UserCancellationWithoutTimeout",
                new() { ["REGULAR_CANCEL_MARKER"] = regularMarkerPath });

            timedResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            timedResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
            timedResult.AssertOutputContains("UserCancellationWithTimeout");
            Assert.AreEqual("continued after cancellation", File.ReadAllText(timedMarkerPath));

            regularResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            regularResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
            regularResult.AssertOutputContains("UserCancellationWithoutTimeout");
            Assert.AreEqual("continued", File.ReadAllText(regularMarkerPath));
        }
        finally
        {
            File.Delete(timedMarkerPath);
            File.Delete(regularMarkerPath);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ThreadAbort_OnNetFrameworkAbortsTheTimeoutTest()
    {
        string markerPath = Path.Combine(Path.GetTempPath(), $"mstest-thread-abort-{Guid.NewGuid():N}.txt");
        try
        {
            TestHostResult result = await ExecuteAsync(
                TargetFrameworks.NetFramework[0],
                "ThreadAbortAbortsTimeoutTest",
                new() { ["THREAD_ABORT_MARKER"] = markerPath });

            result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            result.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
            result.AssertOutputContains("ThreadAbortAbortsTimeoutTest");
            Assert.IsFalse(File.Exists(markerPath), "Execution should not continue after Thread.Abort.");
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    private async Task<TestHostResult> ExecuteAsync(
        string tfm,
        string methodName,
        Dictionary<string, string?> environmentVariables)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        return await testHost.ExecuteAsync(
            $"--filter Name={methodName} --output Detailed",
            environmentVariables,
            cancellationToken: TestContext.CancellationToken);
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "LegacyTimeoutBehavior";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file LegacyTimeoutBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <LangVersion>preview</LangVersion>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file TimeoutCases.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegacyTimeoutBehavior;

#pragma warning disable CS0618
[TestClass]
public class TimeoutCases
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(1000)]
    public void TimeoutExpirationCancelsToken()
    {
        var worker = new Thread(() =>
        {
            try
            {
                Task.Delay(100_000).Wait(TestContext.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                File.WriteAllText(Environment.GetEnvironmentVariable("TIMEOUT_MARKER")!, "cancellation observed");
            }
        });

        worker.Start();
        worker.Join();
    }

    [TestMethod]
    [Timeout(int.MaxValue)]
    public void UserCancellationWithTimeout()
    {
        TestContext.CancellationTokenSource.Cancel();
        File.WriteAllText(Environment.GetEnvironmentVariable("TIMED_CANCEL_MARKER")!, "continued after cancellation");
        Assert.Fail("A timeout test should have been aborted.");
    }

    [TestMethod]
    public void UserCancellationWithoutTimeout()
    {
        TestContext.CancellationTokenSource.Cancel();
        File.WriteAllText(Environment.GetEnvironmentVariable("REGULAR_CANCEL_MARKER")!, "continued");
        Assert.Fail("Expected failure proves execution continued after cancellation.");
    }

#if NETFRAMEWORK
    [TestMethod]
    [Timeout(int.MaxValue)]
    public void ThreadAbortAbortsTimeoutTest()
    {
        Thread.CurrentThread.Abort();
        File.WriteAllText(Environment.GetEnvironmentVariable("THREAD_ABORT_MARKER")!, "continued unexpectedly");
    }
#endif
}
""";
    }

    public TestContext TestContext { get; set; } = default!;
}
