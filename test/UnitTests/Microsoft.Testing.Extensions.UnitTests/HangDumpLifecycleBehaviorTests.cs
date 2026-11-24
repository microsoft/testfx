// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
#if !NETCOREAPP
using Microsoft.Testing.Extensions.Diagnostics.Resources;
#endif
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Logging;
#if !NETCOREAPP
using Microsoft.Testing.Platform.OutputDevice;
#endif
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpLifecycleBehaviorTests
{
    [TestMethod]
    public async Task BeforeStart_InvalidTimeoutFailsBeforeExternalWork()
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpTimeoutOptionName] = ["not-a-duration"];
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();

        FormatException exception = await Assert.ThrowsExactlyAsync<FormatException>(
            () => handler.BeforeTestHostProcessStartAsync(CancellationToken.None));

        Assert.Contains("The value 'not-a-duration' is not a valid time string.", exception.Message);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.EnvironmentMock.Invocations);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Startup_DefaultThirtyMinuteTimeout")]
    [DataRow(1, DisplayName = "Startup_ExplicitMillisecondTimeoutAndFileName")]
    [DataRow(2, DisplayName = "Startup_BestEffortTriageRuntimeMapping")]
    [DataRow(3, DisplayName = "Startup_ExplicitTypePrecedesBestEffortType")]
    public async Task BeforeStart_LoggingFailurePreservesConfiguredTimeoutAndTypeDiagnostics(int scenario)
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];
        if (scenario == 1)
        {
            fixture.Options[HangDumpCommandLineProvider.HangDumpTimeoutOptionName] = ["1500ms"];
            fixture.Options[HangDumpCommandLineProvider.HangDumpTypeOptionName] = ["None"];
            fixture.Options[HangDumpCommandLineProvider.HangDumpFileNameOptionName] = ["nested/{pname}_{pid}.dmp"];
        }
        else if (scenario is 2 or 3)
        {
            fixture.Options[HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName] = ["Triage"];
            if (scenario == 3)
            {
                fixture.Options[HangDumpCommandLineProvider.HangDumpTypeOptionName] = ["Mini"];
            }
        }

        var failure = new IOException("The startup diagnostic sink is unavailable.");
        fixture.Logger
            .Setup(x => x.LogAsync(LogLevel.Information, It.IsAny<string>(), null, It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, exception, _) => fixture.Logs.Add((level, message, exception)))
            .ThrowsAsync(failure);
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();
        using var cancellation = new CancellationTokenSource();

        // Startup logging is not best-effort. Its failure must surface before the concrete server
        // is bound; this exercises configuration/diagnostics without creating an IPC endpoint.
        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
            () => handler.BeforeTestHostProcessStartAsync(cancellation.Token));

        Assert.AreSame(failure, actual);
        Assert.AreSequenceEqual(
            [scenario == 1 ? "Hang dump timeout setup 00:00:01.5000000." : "Hang dump timeout setup 00:30:00."],
            fixture.Logs.Select(log => log.Message));
#if NETCOREAPP
        Assert.IsEmpty(fixture.Displays);
#else
        if (scenario == 2)
        {
            Assert.HasCount(1, fixture.Displays);
            Assert.AreSame(handler, fixture.Displays[0].Producer);
            Assert.AreEqual(cancellation.Token, fixture.Displays[0].Token);
            Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(fixture.Displays[0].Data);
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeIfSupportedFallbackInfoMessage, "Triage", "Mini"),
                ((FormattedTextOutputDeviceData)fixture.Displays[0].Data).Text);
        }
        else
        {
            Assert.IsEmpty(fixture.Displays);
        }
#endif
        Assert.IsEmpty(fixture.EnvironmentMock.Invocations);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Deadline_ExplicitOffsetAndDefaultMargin")]
    [DataRow(1, DisplayName = "Deadline_CustomNinetySecondMargin")]
    [DataRow(2, DisplayName = "Deadline_ZeroMargin")]
    [DataRow(3, DisplayName = "Deadline_SubtractionSaturatesAtYearOne")]
    [DataRow(4, DisplayName = "Deadline_InvalidMarginFallsBackToThirtySeconds")]
    public async Task BeforeStart_DeadlineDiagnosticUsesConfiguredMarginAndSaturates(int scenario)
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.EnvironmentValues["TESTINGPLATFORM_DEADLINE"] = scenario == 3
            ? "0001-01-01T00:00:00Z"
            : "2026-10-30T14:00:00+02:00";
        string expectedInstant;
        switch (scenario)
        {
            case 1:
                fixture.EnvironmentValues["TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"] = "90s";
                expectedInstant = "2026-10-30T11:58:30.0000000+00:00";
                break;
            case 2:
                fixture.EnvironmentValues["TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"] = "0";
                expectedInstant = "2026-10-30T12:00:00.0000000+00:00";
                break;
            case 3:
                fixture.EnvironmentValues["TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"] = "45s";
                expectedInstant = "0001-01-01T00:00:00.0000000+00:00";
                break;
            case 4:
                fixture.EnvironmentValues["TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"] = "not-a-duration";
                expectedInstant = "2026-10-30T11:59:30.0000000+00:00";
                break;
            default:
                expectedInstant = "2026-10-30T11:59:30.0000000+00:00";
                break;
        }

        var failure = new IOException("The deadline diagnostic sink is unavailable.");
        fixture.Logger
            .Setup(x => x.LogAsync(
                LogLevel.Information,
                It.Is<string>(message => message.StartsWith("Hang dump deadline setup ", StringComparison.Ordinal)),
                null,
                It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, message, exception, _) => fixture.Logs.Add((level, message, exception)))
            .ThrowsAsync(failure);
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(
            () => handler.BeforeTestHostProcessStartAsync(CancellationToken.None));

        Assert.AreSame(failure, actual);
        Assert.AreSequenceEqual(
            ["Hang dump timeout setup 00:30:00.", $"Hang dump deadline setup {expectedInstant}."],
            fixture.Logs.Select(log => log.Message));
        fixture.EnvironmentMock.Verify(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DEADLINE"), Times.Once);
        fixture.EnvironmentMock.Verify(x => x.GetEnvironmentVariable("TESTINGPLATFORM_DEADLINE_DUMP_MARGIN"), Times.Once);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Started_WithoutPreparedServerRejectsBeforeInspectingProcess()
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();
        var process = new Mock<ITestHostProcessInformation>(MockBehavior.Strict);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.OnTestHostProcessStartedAsync(process.Object, CancellationToken.None));

        Assert.Contains("Unexpected state", exception.Message);
        process.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Scheduler.Invocations);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.Displays);
    }

    [TestMethod]
    public async Task Exit_PreCanceledTokenStopsBeforeInspectingProcess()
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();
        var process = new Mock<ITestHostProcessInformation>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.OnTestHostProcessExitedAsync(process.Object, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        process.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Publications);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Logs);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Exit_GracefulWithoutDump")]
    [DataRow(false, DisplayName = "Exit_UngracefulWithoutDump")]
    public async Task Exit_WithoutDumpPublishesNoArtifacts(bool graceful)
    {
        using var fixture = new DumpBehaviorFixture();
        using HangDumpProcessLifetimeHandler handler = fixture.CreateHangHandler();

        await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 73, graceful), CancellationToken.None);

        Assert.AreSequenceEqual(
            graceful ? [] : ["Testhost didn't exit gracefully '73')"],
            fixture.Logs.Select(log => log.Message));
        if (!graceful)
        {
            Assert.AreEqual(LogLevel.Debug, fixture.Logs[0].Level);
        }

        Assert.IsEmpty(fixture.Publications);
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.ProcessHandler.Invocations);
        Assert.IsEmpty(fixture.Scheduler.Invocations);
    }
}
