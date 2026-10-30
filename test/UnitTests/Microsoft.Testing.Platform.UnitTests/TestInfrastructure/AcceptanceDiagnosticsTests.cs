// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests.TestInfrastructure;

[TestClass]
public sealed class AcceptanceDiagnosticsTests
{
    [TestMethod]
    [DataRow(false, 7, 123, "acceptance-host", DisplayName = "ProcessSummaryTestHost")]
    [DataRow(false, 0, 42, "acceptance-host", DisplayName = "ProcessSummaryZeroAttempts")]
    [DataRow(true, 7, 123, "artifact-worker", DisplayName = "ProcessSummaryArtifactWorker")]
    public void FormatProcessSummary_AttributesModuleFrameworkProcessAndRole(bool isArtifactWorker, int attempts, int processId, string role)
    {
        string actual = AcceptanceDiagnostics.FormatProcessSummary("Tests", "net8.0", processId, attempts, isArtifactWorker);

        Assert.AreEqual($"Process launch attempts: {attempts} [module=Tests, framework=net8.0, pid={processId}, role={role}]", actual);
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "ProcessSummaryNegativeExitIsSilent")]
    [DataRow(2, DisplayName = "ProcessSummaryTestFailureIsSilent")]
    public void WriteProcessSummary_NonzeroExitWritesNothing(int exitCode)
    {
        List<string> lines = [];

        AcceptanceDiagnostics.WriteProcessSummary([], exitCode, 7, lines.Add);
        AcceptanceDiagnostics.WriteProcessSummary(["internal-merge-artifacts"], exitCode, 7, lines.Add);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    [DataRow("empty", "acceptance-host", DisplayName = "ProcessSummaryNoTool")]
    [DataRow("leading", "artifact-worker", DisplayName = "ProcessSummaryLeadingArtifactTool")]
    [DataRow("later", "acceptance-host", DisplayName = "ProcessSummaryUnrelatedArgument")]
    [DataRow("case", "acceptance-host", DisplayName = "ProcessSummaryCaseSensitiveTool")]
    [DataRow("substring", "acceptance-host", DisplayName = "ProcessSummaryNotSubstringMatch")]
    public void WriteProcessSummary_UsesLiveIdentityAndOnlyAnExactLeadingArtifactTool(string scenario, string role)
    {
        string[] args = scenario switch
        {
            "empty" => [],
            "leading" => ["internal-merge-artifacts", "--manifest", "manifest.json"],
            "later" => ["--name", "internal-merge-artifacts"],
            "case" => ["Internal-Merge-Artifacts"],
            "substring" => ["internal-merge-artifacts-extra"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        List<string> lines = [];

        AcceptanceDiagnostics.WriteProcessSummary(args, exitCode: 0, processLaunchAttempts: 7, lines.Add);

#if NETCOREAPP
        int processId = Environment.ProcessId;
        string? targetFramework = AppContext.TargetFrameworkName;
#else
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        int processId = process.Id;
        string? targetFramework = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
#endif
        Assert.AreEqual(
            $"Process launch attempts: 7 [module={Assembly.GetEntryAssembly()!.GetName().Name}, framework={targetFramework}, pid={processId.ToString(CultureInfo.InvariantCulture)}, role={role}]",
            Assert.ContainsSingle(lines));
    }
}
