// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private void AddResultSummary(XElement testRun, string resultSummaryOutcome, string runDeploymentRoot, string testHostCrashInfo, int exitCode, SummaryCounts summaryCounts, List<string> attachmentWarnings, bool isTestHostCrashed = false)
    {
        // Unlike VSTest, we do not add an Output/StdOut element to ResultSummary.
        // VSTest adds mainly two things in that element:
        // 1. Skipped test messages (see AddRunLevelInformationalMessage call in HandleSkippedTest in VSTest's TrxLogger implementation)
        // 2. Messages published with TestMessageLevel.Informational.
        var resultSummary = new XElement(
            NamespaceUri + "ResultSummary",
            new XAttribute("outcome", resultSummaryOutcome));
        testRun.Add(resultSummary);

        // NOTE: Looking at VSTest implementation:
        // 1. timeout is always set to 0 (it seems ObjectModel doesn't have the concept of timeout at all)
        // 2. Skipped tests are not counted in VSTest implementation.
        //    An informative message is added to indicate that test was skipped.
        // While what we have is reasonable, tooling implemented around might have been relying on VSTest implementation details.
        var counters = new XElement(
            NamespaceUri + "Counters",
            new XAttribute("total", summaryCounts.Passed + summaryCounts.Failed + summaryCounts.Skipped + summaryCounts.Timedout),
            new XAttribute("executed", summaryCounts.Passed + summaryCounts.Failed),
            new XAttribute("passed", summaryCounts.Passed),
            new XAttribute("failed", summaryCounts.Failed),
            new XAttribute("error", 0),
            new XAttribute("timeout", summaryCounts.Timedout),
            new XAttribute("aborted", 0),
            new XAttribute("inconclusive", 0),
            new XAttribute("passedButRunAborted", 0),
            new XAttribute("notRunnable", 0),
            new XAttribute("notExecuted", summaryCounts.Skipped),
            new XAttribute("disconnected", 0),
            new XAttribute("warning", 0),
            new XAttribute("completed", 0),
            new XAttribute("inProgress", 0),
            new XAttribute("pending", 0));
        resultSummary.Add(counters);

        // VSTest adds two additional things to RunInfos.
        // 1. Messages published with TestMessageLevel.Warning or TestMessageLevel.Error
        // 2. Errors when constructing result files.
        // In addition, in these cases, it turns TRX outcome to error.
        XElement? runInfos = null;
        if (isTestHostCrashed)
        {
            AddRunInfo(resultSummary, ref runInfos, "Error", testHostCrashInfo);
        }
        else if (exitCode != (int)ExitCode.Success)
        {
            AddRunInfo(resultSummary, ref runInfos, "Error", $"Exit code indicates failure: '{exitCode}'. Please refer to https://aka.ms/testingplatform/exitcodes for more information.");
        }

        if (_artifactsByExtension.Count != 0)
        {
            // VSTest seems to also add a ResultFiles element, and not only CollectorDataEntries.
            // Whether this reporter should also emit ResultFiles here could be revisited by referencing VSTest's Converter.ToCollectionEntries and Converter.ToResultFiles.
            var collectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");
            resultSummary.Add(collectorDataEntries);

            AddArtifactsToCollection(_artifactsByExtension, collectorDataEntries, runDeploymentRoot, attachmentWarnings);
        }

        foreach (string attachmentWarning in attachmentWarnings)
        {
            AddRunInfo(resultSummary, ref runInfos, "Warning", attachmentWarning);
        }
    }

    private void AddRunInfo(XElement resultSummary, ref XElement? runInfos, string outcome, string text)
    {
        if (runInfos is null)
        {
            runInfos = new XElement(NamespaceUri + "RunInfos");
            resultSummary.Add(runInfos);
        }

        var runInfo = new XElement(
            NamespaceUri + "RunInfo",
            new XAttribute("computerName", _environment.MachineName),
            new XAttribute("outcome", outcome),
            new XAttribute("timestamp", _clock.UtcNow.DateTime));
        runInfo.Add(new XElement(NamespaceUri + "Text", RemoveInvalidXmlChar(text)));
        runInfos.Add(runInfo);
    }

    private static void AddTestLists(XElement testRun)
    {
        var testLists = new XElement(
            "TestLists",
            new XElement(
                "TestList",
                // NOTE: VSTest localizes this string.
                new XAttribute("name", "Results Not in a List"),
                new XAttribute("id", UncategorizedTestListId)),
            new XElement(
                "TestList",
                new XAttribute("name", "All Loaded Results"),
                // parent of all categories (fake, not real category).
                new XAttribute("id", new Guid("19431567-8539-422a-85D7-44EE4E166BDA"))));

        testRun.Add(testLists);
    }

    private static string AddTestSettings(XElement testRun, string testRunName)
    {
        var testSettings = new XElement(
            "TestSettings",
            new XAttribute("name", "default"),
            new XAttribute("id", Guid.NewGuid()));
        string runDeploymentRoot = ReportFileNameSanitizer.ReplaceInvalidFileNameChars(testRunName);
        testSettings.Add(new XElement("Deployment", new XAttribute("runDeploymentRoot", runDeploymentRoot)));
        testRun.Add(testSettings);
        return runDeploymentRoot;
    }

    private void AddTimes(XElement testRun)
    {
        var times = new XElement(
            "Times",
            new XAttribute("creation", _testStartTime),
            new XAttribute("queuing", _testStartTime),
            new XAttribute("start", _testStartTime),
            new XAttribute("finish", _clock.UtcNow));
        testRun.Add(times);
    }
}
