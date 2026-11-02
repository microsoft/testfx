// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsOrchestratorBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "Orchestrator_MissingConfiguration")]
    [DataRow(true, DisplayName = "Orchestrator_AncestorOwnsRun")]
    public async Task BeforeRun_InheritedOrInvalidConfigurationDoesNotCreateRun(bool inherited)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        if (inherited)
        {
            fixture.Variables[AzureDevOpsConstants.TestRunIdEnvironmentVariableName] = "123:900";
        }
        else
        {
            fixture.Variables["SYSTEM_ACCESSTOKEN"] = null;
        }

        AzureDevOpsTestRunOrchestratorLifetime lifetime = fixture.Orchestrator();
        Assert.IsTrue(await lifetime.IsEnabledAsync());

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0, CancellationToken.None);

        Assert.IsNull(lifetime.RunId);
        fixture.Client.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Opens);
        Assert.IsEmpty(fixture.Displays);
        Assert.HasCount(inherited ? 0 : 1, fixture.Logs);
        if (!inherited)
        {
            Assert.Contains("SYSTEM_ACCESSTOKEN", fixture.LogText);
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Orchestrator_RunHandoffFailure")]
    [DataRow(1, DisplayName = "Orchestrator_MapHandoffFailure")]
    [DataRow(2, DisplayName = "Orchestrator_ProgressOutputFailure")]
    public async Task Handoff_FailureDoesNotLoseRunFinalization(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        string failedVariable = failure == 0 ? AzureDevOpsConstants.TestRunIdEnvironmentVariableName : AzureDevOpsConstants.ResultMapPathEnvironmentVariableName;
        if (failure < 2)
        {
            fixture.Environment.Setup(value => value.SetEnvironmentVariable(failedVariable, It.IsNotNull<string>()))
                .Throws(new SecurityException("handoff denied"));
        }
        else
        {
            fixture.DisplayError = _ => new IOException("progress output denied");
        }

        AzureDevOpsTestRunOrchestratorLifetime lifetime = fixture.Orchestrator();
        await lifetime.BeforeRunAsync(CancellationToken.None);
        Assert.AreEqual(77, lifetime.RunId);

        await lifetime.AfterRunAsync(2, CancellationToken.None);
        await lifetime.AfterRunAsync(2, CancellationToken.None);

        fixture.Client.Verify(value => value.CreateTestRunAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, "Completed", It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)), Times.Once);
        Assert.IsNull(lifetime.RunId);
        Assert.IsNull(fixture.Variables[AzureDevOpsConstants.TestRunIdEnvironmentVariableName]);
        Assert.IsNull(fixture.Variables[AzureDevOpsConstants.ResultMapPathEnvironmentVariableName]);
        Assert.Contains(failure == 2 ? "progress output denied" : "handoff denied", fixture.LogText);
        if (failure == 2)
        {
            (IOutputDeviceDataProducer Owner, IOutputDeviceData Data, CancellationToken Token) display = Assert.ContainsSingle(fixture.DisplayAttempts);
            Assert.AreSame(lifetime, display.Owner);
            Assert.AreEqual("AzureDevOpsTestRunOrchestratorLifetime", display.Owner.Uid);
            Assert.AreEqual(AzureDevOpsResources.DisplayName, display.Owner.DisplayName);
            Assert.AreEqual(AzureDevOpsResources.Description, display.Owner.Description);
            Assert.IsNotEmpty(display.Owner.Version);
        }
    }

    [TestMethod]
    [DataRow(0, 0, false, "Completed", DisplayName = "Orchestrator_IoCleanupFailure")]
    [DataRow(1, 2, false, "Completed", DisplayName = "Orchestrator_AccessCleanupFailure")]
    [DataRow(2, 8, false, "Aborted", DisplayName = "Orchestrator_SecurityCleanupFailure")]
    [DataRow(3, 0, true, "Aborted", DisplayName = "Orchestrator_WithdrawHandoffFailure")]
    [DataRow(4, 0, false, "Completed", DisplayName = "Orchestrator_FinalizationAndDiagnosticFailure")]
    public async Task AfterRun_CleanupFailuresDoNotReplaceCompletion(int failure, int exitCode, bool canceled, string state)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = fixture.Orchestrator();
        await lifetime.BeforeRunAsync(CancellationToken.None);
        string map = fixture.Variables[AzureDevOpsConstants.ResultMapPathEnvironmentVariableName]!.Substring(4);
        fixture.PutText(map, "owned map");
        if (failure < 3)
        {
            fixture.BeforeDelete = path =>
            {
                if (path == map)
                {
                    throw failure switch
                    {
                        0 => new IOException("map deletion denied"),
                        1 => new UnauthorizedAccessException("map deletion denied"),
                        _ => new SecurityException("map deletion denied"),
                    };
                }
            };
        }
        else if (failure == 3)
        {
            fixture.Environment.Setup(value => value.SetEnvironmentVariable(It.IsAny<string>(), null))
                .Throws(new SecurityException("withdrawal denied"));
        }
        else
        {
            fixture.Client.Setup(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, state, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("cleanup token expired"));
            fixture.DisplayError = data => data is WarningMessageOutputDeviceData ? new IOException("warning display denied") : null;
            fixture.LogError = new IOException("warning logger denied");
        }

        using var cancellation = new CancellationTokenSource();
        if (canceled)
        {
            cancellation.Cancel();
        }

        await lifetime.AfterRunAsync(exitCode, cancellation.Token);
        await lifetime.AfterRunAsync(exitCode, cancellation.Token);

        Assert.IsNull(lifetime.RunId);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), 77, state, It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)), Times.Once);
        fixture.Environment.Verify(value => value.SetEnvironmentVariable(AzureDevOpsConstants.TestRunIdEnvironmentVariableName, null), Times.Once);
        fixture.Environment.Verify(value => value.SetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName, null), Times.Once);
        Assert.AreEqual(failure < 3, fixture.Files.ContainsKey(map));
        if (failure == 4)
        {
            Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress, fixture.LogText);
            Assert.Contains("warning display denied", fixture.LogText);
        }
        else
        {
            Assert.Contains(failure == 3 ? "withdrawal denied" : "map deletion denied", fixture.LogText);
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Orchestrator_CreateFailure")]
    [DataRow(1, DisplayName = "Orchestrator_InternalCreateCancellation")]
    [DataRow(2, DisplayName = "Orchestrator_CallerCancellation")]
    public async Task BeforeRun_AcquisitionFailureReportsWithoutHalfInitializedState(int failure)
    {
        using var fixture = new CiReporterFixture();
        fixture.EnableAzureDevOps();
        using var cancellation = new CancellationTokenSource();
        if (failure == 2)
        {
            cancellation.Cancel();
        }

        fixture.Client.Setup(value => value.CreateTestRunAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure == 1 ? new TaskCanceledException("internal create timeout") : new IOException("create failed"));
        AzureDevOpsTestRunOrchestratorLifetime lifetime = fixture.Orchestrator();

        if (failure == 2)
        {
            OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => lifetime.BeforeRunAsync(cancellation.Token));
            Assert.AreEqual(cancellation.Token, error.CancellationToken);
            Assert.IsEmpty(fixture.Displays);
        }
        else
        {
            await lifetime.BeforeRunAsync(cancellation.Token);
            WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Displays));
            Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, warning.Message);
            Assert.Contains(failure == 1 ? "internal create timeout" : "create failed", warning.Message);
        }

        await lifetime.AfterRunAsync(0, CancellationToken.None);
        Assert.IsNull(lifetime.RunId);
        fixture.Client.Verify(value => value.UpdateTestRunStateAsync(It.IsAny<AzureDevOpsPublishConfiguration>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsFalse(fixture.Variables.ContainsKey(AzureDevOpsConstants.TestRunIdEnvironmentVariableName));
    }
}
