// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CoverageThresholdPolicyDeferralTests
{
    private static string EnvName(string pid)
        => $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_COVERAGEPOLICY}_{pid}";

    private static TestCommandLineOptions Options(bool line, bool branch, string[]? controllerPid)
    {
        Dictionary<string, string[]> options = [];
        if (line)
        {
            options[PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = ["80"];
        }

        if (branch)
        {
            options[PlatformCommandLineProvider.CoverageThresholdBranchOptionKey] = ["80"];
        }

        if (controllerPid is not null)
        {
            options[PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = controllerPid;
        }

        return new(options);
    }

    private static IEnvironment Env(string? pid, string? value)
    {
        Mock<IEnvironment> env = new();
        if (pid is not null)
        {
            env.Setup(e => e.GetEnvironmentVariable(EnvName(pid))).Returns(value);
        }

        return env.Object;
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void IsDeferredToController_AnyThresholdOptionWithMatchingEnvironment_ReturnsTrue(bool line, bool branch)
        => Assert.IsTrue(CoverageThresholdPolicy.IsDeferredToController(Options(line, branch, ["123"]), Env("123", "1")));

    [TestMethod]
    public void IsDeferredToController_NoThresholdOption_ReturnsFalse()
        => Assert.IsFalse(CoverageThresholdPolicy.IsDeferredToController(Options(false, false, ["123"]), Env("123", "1")));

    [TestMethod]
    public void IsDeferredToController_NoControllerPid_ReturnsFalse()
        => Assert.IsFalse(CoverageThresholdPolicy.IsDeferredToController(Options(true, true, null), Env("123", "1")));

    [TestMethod]
    public void IsDeferredToController_MoreThanOneControllerPid_ReturnsFalse()
        => Assert.IsFalse(CoverageThresholdPolicy.IsDeferredToController(Options(true, true, ["123", "456"]), Env("123", "1")));

    [TestMethod]
    [DataRow("0")]
    [DataRow("2")]
    [DataRow("true")]
    [DataRow("")]
    [DataRow(null)]
    public void IsDeferredToController_EnvironmentValueIsNotOne_ReturnsFalse(string? value)
        => Assert.IsFalse(CoverageThresholdPolicy.IsDeferredToController(Options(true, true, ["123"]), Env("123", value)));

    [TestMethod]
    public void IsDeferredToController_EnvironmentIsForAnotherPid_ReturnsFalse()
        => Assert.IsFalse(CoverageThresholdPolicy.IsDeferredToController(Options(true, true, ["123"]), Env("456", "1")));
}

[TestClass]
public sealed class CoverageThresholdPolicyEvaluationTests
{
    private static TestCommandLineOptions Line(string value)
        => new(new Dictionary<string, string[]> { [PlatformCommandLineProvider.CoverageThresholdLineOptionKey] = [value] });

    [TestMethod]
    public void Evaluate_NoCoverableData_ReportsEmptyMeasurementAndNoCoverableData()
    {
        SessionUid session = new("s");
        CoverageScopeSummary summary = new(session, CoverageScope.Overall, [new(CoverageMetric.Line, 0, 0, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> thresholds, IReadOnlyList<string> errors) = CoverageThresholdPolicy.Evaluate(Line("0"), [summary], session);

        TestCoverageThresholdMessage threshold = Assert.ContainsSingle(thresholds);
        Assert.IsFalse(threshold.HasCoverableData);
        Assert.IsFalse(threshold.Passed);
        Assert.AreEqual(0d, threshold.ActualPercentage);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.CoverageThresholdEmptyMeasurement, PlatformCommandLineProvider.CoverageThresholdLineOptionKey),
            Assert.ContainsSingle(errors));
    }

    [TestMethod]
    public void Evaluate_WithCoverableData_ReportsHasCoverableDataAndExactPercentages()
    {
        SessionUid session = new("s");
        CoverageScopeSummary summary = new(session, CoverageScope.Overall, [new(CoverageMetric.Line, 1, 4, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> thresholds, IReadOnlyList<string> errors) = CoverageThresholdPolicy.Evaluate(Line("50"), [summary], session);

        TestCoverageThresholdMessage threshold = Assert.ContainsSingle(thresholds);
        Assert.IsTrue(threshold.HasCoverableData);
        Assert.AreEqual(25d, threshold.ActualPercentage);
        Assert.AreEqual(50d, threshold.RequiredPercentage);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CoverageThresholdNotMet"), PlatformCommandLineProvider.CoverageThresholdLineOptionKey, "25", "50"),
            Assert.ContainsSingle(errors));
    }

    [TestMethod]
    public void Evaluate_NullSessionWithSingleOverall_UsesTheOverallSession()
    {
        SessionUid session = new("overall-session");
        CoverageScopeSummary summary = new(session, CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> thresholds, _) = CoverageThresholdPolicy.Evaluate(Line("80"), [summary], sessionUid: null);

        Assert.AreEqual(session, Assert.ContainsSingle(thresholds).SessionUid);
    }

    [TestMethod]
    public void Evaluate_NullSessionAndNoOverall_UsesCoverageSession()
    {
        (IReadOnlyList<TestCoverageThresholdMessage> thresholds, _) = CoverageThresholdPolicy.Evaluate(Line("80"), [], sessionUid: null);

        Assert.AreEqual(new SessionUid("coverage"), Assert.ContainsSingle(thresholds).SessionUid);
    }

    [TestMethod]
    public void Evaluate_ExplicitSession_IsPreferredOverOverallSession()
    {
        CoverageScopeSummary summary = new(new SessionUid("overall-session"), CoverageScope.Overall, [new(CoverageMetric.Line, 10, 10, "collector")]);

        (IReadOnlyList<TestCoverageThresholdMessage> thresholds, _) = CoverageThresholdPolicy.Evaluate(Line("80"), [summary], new SessionUid("overall-session"));

        Assert.AreEqual(new SessionUid("overall-session"), Assert.ContainsSingle(thresholds).SessionUid);
    }

    [TestMethod]
    public void ExitCodePolicy_Apply_NonSuccessExitCodeIsKeptEvenWithThresholdFailure()
    {
        ServiceProvider services = new();
        Mock<ITestCoverageResult> coverage = new();
        coverage.SetupGet(c => c.HasThresholdFailure).Returns(true);
        services.AddService(coverage.Object);

        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, CoverageThresholdExitCodePolicy.Apply((int)ExitCode.AtLeastOneTestFailed, services));
    }

    [TestMethod]
    public void ExitCodePolicy_Apply_SuccessWithThresholdFailure_ReturnsCoverageThresholdFailed()
    {
        ServiceProvider services = new();
        Mock<ITestCoverageResult> coverage = new();
        coverage.SetupGet(c => c.HasThresholdFailure).Returns(true);
        services.AddService(coverage.Object);

        Assert.AreEqual((int)ExitCode.CoverageThresholdFailed, CoverageThresholdExitCodePolicy.Apply((int)ExitCode.Success, services));
    }

    [TestMethod]
    public void ExitCodePolicy_Apply_SuccessWithoutFailureOrService_ReturnsSuccess()
    {
        ServiceProvider withPassing = new();
        Mock<ITestCoverageResult> coverage = new();
        coverage.SetupGet(c => c.HasThresholdFailure).Returns(false);
        withPassing.AddService(coverage.Object);

        Assert.AreEqual((int)ExitCode.Success, CoverageThresholdExitCodePolicy.Apply((int)ExitCode.Success, withPassing));
        Assert.AreEqual((int)ExitCode.Success, CoverageThresholdExitCodePolicy.Apply((int)ExitCode.Success, new ServiceProvider()));
    }
}
