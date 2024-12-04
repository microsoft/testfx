// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml.Linq;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxCompareTool : ITool, IOutputDeviceDataProducer
{
    public const string ToolName = "ms-trxcompare";
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IExtension _extension;
    private readonly IOutputDevice _outputDisplay;
    private readonly ITask _task;

    public TrxCompareTool(ICommandLineOptions commandLineOptions, IExtension extension, IOutputDevice outputDisplay,
        ITask task)
    {
        _commandLineOptions = commandLineOptions;
        _extension = extension;
        _outputDisplay = outputDisplay;
        _task = task;
    }

    /// <inheritdoc />
    public string Name { get; } = ToolName;

    /// <inheritdoc />
    public bool Hidden { get; }

    /// <inheritdoc />
    public string Uid => _extension.Uid;

    /// <inheritdoc />
    public string Version => _extension.Version;

    /// <inheritdoc />
    public string DisplayName => _extension.DisplayName;

    /// <inheritdoc />
    public string Description => _extension.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<int> RunAsync()
    {
        if (!_commandLineOptions.TryGetOptionArgumentList(TrxCompareToolCommandLine.BaselineTrxOptionName, out string[]? baselineFilePaths)
            || !_commandLineOptions.TryGetOptionArgumentList(TrxCompareToolCommandLine.TrxToCompareOptionName, out string[]? comparedFilePaths))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        XNamespace trxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        List<(string TestName, string Outcome, string Storage)> baseLineResults = new();
        List<string> baseLineIssues = new();
        List<(string TestName, string Outcome, string Storage)> comparedResults = new();
        List<string> comparedIssues = new();
        await _task.WhenAll(
            _task.Run(() => CollectEntriesAndErrors(baselineFilePaths[0], trxNamespace, baseLineResults, baseLineIssues)),
            _task.Run(() => CollectEntriesAndErrors(comparedFilePaths[0], trxNamespace, comparedResults, comparedIssues)));

        StringBuilder outputBuilder = new();
        AppendResultsAndIssues("Baseline", baselineFilePaths[0], baseLineResults, baseLineIssues, outputBuilder);
        AppendResultsAndIssues("Compared TRX", comparedFilePaths[0], comparedResults, comparedIssues, outputBuilder);

        if (AreMatchingTrxFiles(baseLineResults, comparedResults, outputBuilder))
        {
            await _outputDisplay.DisplayAsync(this, new TextOutputDeviceData(outputBuilder.ToString()));
            return ExitCodes.Success;
        }
        else
        {
            await _outputDisplay.DisplayAsync(this, new TextOutputDeviceData(outputBuilder.ToString()));
            return ExitCodes.GenericFailure;
        }
    }

    private static bool AreMatchingTrxFiles(
        List<(string TestName, string Outcome, string Storage)> baseLineResults,
        List<(string TestName, string Outcome, string Storage)> comparedResults,
        StringBuilder outputBuilder)
    {
        bool checkFailed = false;
        outputBuilder.AppendLine("--- Comparing TRX files ---");

        IEnumerable<((string TestName, string Outcome, string Storage), string Source)> trxEntries =
            baseLineResults.Select(tuple => (tuple, "baseline"))
            .Concat(comparedResults.Select(tuple => (tuple, "other")))
            .OrderBy(x => x.tuple.TestName);

        foreach (((string TestName, string Outcome, string Storage) sourceTrx, string entrySource) in trxEntries)
        {
            string otherSource = entrySource == "baseline" ? "other" : "baseline";
            IEnumerable<(string MatchingTestName, string MatchingOutcome, string MatchingStorage)> matchingEntries =
                entrySource == "baseline"
                    ? comparedResults.Where(x => x.TestName == sourceTrx.TestName)
                    : baseLineResults.Where(x => x.TestName == sourceTrx.TestName);
            if (!matchingEntries.Any())
            {
                checkFailed = true;
                outputBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  - Test '{sourceTrx.TestName}' is missing inside the trx '{otherSource}'");
                continue;
            }

            if (matchingEntries.Skip(1).Any())
            {
                checkFailed = true;
                outputBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  - Test '{sourceTrx.TestName}' is found multiple times inside the trx '{otherSource}'");
                continue;
            }

            (string otherTestName, string otherOutcome, string otherStorage) = matchingEntries.First();
            if (sourceTrx.Outcome != otherOutcome)
            {
                checkFailed = true;
                outputBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  - Test '{sourceTrx.TestName}' has a different outcome. Got '{otherOutcome}', expected '{sourceTrx.Outcome}'");
            }
        }

        outputBuilder.AppendLine();
        if (checkFailed)
        {
            outputBuilder.AppendLine("Comparison check failed!");
        }
        else
        {
            outputBuilder.AppendLine("Comparison check succeeded!");
        }

        return !checkFailed;
    }

    private static void AppendResultsAndIssues(string category, string filePath,
        List<(string TestName, string Outcome, string Storage)> results, List<string> issues, StringBuilder outputBuilder)
    {
        outputBuilder.AppendLine(CultureInfo.InvariantCulture, $"--- {category} ---");
        outputBuilder.AppendLine(CultureInfo.InvariantCulture, $"File '{filePath}'");

        outputBuilder.AppendLine("Issues:");
        foreach (string issue in issues)
        {
            outputBuilder.AppendLine(CultureInfo.InvariantCulture, $"  - {issue}");
        }

        if (issues.Count == 0)
        {
            outputBuilder.AppendLine("  None");
        }

        outputBuilder.AppendLine();
        outputBuilder.AppendLine("Test containers (assemblies):");
        foreach (string? storage in results.Select(x => x.Storage).Distinct())
        {
            outputBuilder.AppendLine(CultureInfo.InvariantCulture, $"  - {storage}");
        }

        outputBuilder.AppendLine();
        outputBuilder.AppendLine("Test results:");
        foreach ((string outcome, int resultCount) in results.GroupBy(x => x.Outcome, (key, results) => (key, results.Count())))
        {
            outputBuilder.AppendLine(CultureInfo.InvariantCulture, $"  - {outcome}: {resultCount}");
        }

        outputBuilder.AppendLine();
    }

    private static void CollectEntriesAndErrors(string trxFile, XNamespace ns, List<(string TestName, string Outcome, string Storage)> results, List<string> issues)
    {
        var trxTestRun = XElement.Parse(File.ReadAllText(trxFile));
        int testResultIndex = 0;
        foreach (XElement testResult in trxTestRun.Elements(ns + "Results").Elements(ns + "UnitTestResult"))
        {
            testResultIndex++;
            string? testId = testResult.Attribute("testId")?.Value;
            if (testId is null)
            {
                issues.Add($"UnitTestResult at index '{testResultIndex}' is missing 'testId' attribute.");
                continue;
            }

            string? testResultTestName = testResult.Attribute("testName")?.Value;
            if (testResultTestName is null)
            {
                issues.Add($"UnitTestResult at index '{testResultIndex}' is missing 'testName' attribute.");
                continue;
            }

            string? testResultOutcome = testResult.Attribute("outcome")?.Value;
            if (testResultOutcome is null)
            {
                issues.Add($"UnitTestResult at index '{testResultIndex}' is missing 'outcome' attribute.");
                continue;
            }

            XElement[] matchingUnitTestDefinitions = trxTestRun
                .Elements(ns + "TestDefinitions")
                .Elements(ns + "UnitTest")
                .Where(x => x.Attribute("id")?.Value == testId)
                .ToArray();
            if (matchingUnitTestDefinitions.Length > 1)
            {
                issues.Add($"Found more than one entry in 'TestDefinitions.UnitTest' matching the test ID '{testId}'.");
                continue;
            }

            if (matchingUnitTestDefinitions.Length == 0)
            {
                issues.Add($"Cannot find any 'TestDefinitions.UnitTest' matching the test ID '{testId}'.");
                continue;
            }

            XElement matchingUnitTestDefinition = matchingUnitTestDefinitions[0];
            string testDefinitionId = matchingUnitTestDefinition.Attribute("id")!.Value;

            string? testDefinitionStorage = matchingUnitTestDefinition.Attribute("storage")?.Value;
            if (testDefinitionStorage is null)
            {
                issues.Add($"Cannot find attribute 'storage' for 'TestDefinitions.UnitTest' with ID '{testDefinitionId}'.");
                continue;
            }

            string? testDefinitionClassName = matchingUnitTestDefinition.Element(ns + "TestMethod")
                ?.Attribute("className")
                ?.Value;
            if (testDefinitionClassName is null)
            {
                issues.Add($"Cannot find attribute 'className' on sub node 'TestMethod' for 'TestDefinitions.UnitTest' with ID '{testDefinitionId}'.");
                continue;
            }

            results.Add((testDefinitionClassName + "." + testResultTestName, testResultOutcome, testDefinitionStorage));
        }
    }
}
