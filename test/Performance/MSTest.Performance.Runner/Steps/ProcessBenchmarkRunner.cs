// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Performance.Runner.Steps;

internal static class ProcessBenchmarkRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Regex MtpSummaryRegex = new(
        @"Test run summary:.*?total:\s*(?<total>\d+).*?failed:\s*(?<failed>\d+).*?succeeded:\s*(?<passed>\d+).*?skipped:\s*(?<skipped>\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex VSTestSummaryRegex = new(
        @"Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public static void ConfigureEnvironment(ProcessStartInfo processStartInfo)
    {
        foreach (string toSkip in WellKnownEnvironmentVariables.ToSkipEnvironmentVariables)
        {
            processStartInfo.EnvironmentVariables.Remove(toSkip);
        }

        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        processStartInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        processStartInfo.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        processStartInfo.EnvironmentVariables["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US";
        processStartInfo.EnvironmentVariables["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        processStartInfo.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
        processStartInfo.EnvironmentVariables["DOTNET_INSTALL_DIR"] = dotnetRoot;
        processStartInfo.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        processStartInfo.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
    }

    public static async Task RunAsync(
        ProcessStartInfo processStartInfo,
        BuildArtifact payload,
        IContext context,
        string executionKind,
        string processResourceScope,
        bool captureProcessResources,
        int numberOfRuns,
        int warmupCount)
    {
        if (numberOfRuns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfRuns));
        }

        if (warmupCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupCount));
        }

        Console.WriteLine(
            $"Process command: '{processStartInfo.FileName} {processStartInfo.Arguments.Trim()}', " +
            $"{warmupCount} warmup and {numberOfRuns} measured runs");

        List<ProcessBenchmarkSample> samples = [];
        for (int i = 0; i < warmupCount + numberOfRuns; i++)
        {
            bool isWarmup = i < warmupCount;
            using Process process = new() { StartInfo = processStartInfo };
            var stopwatch = Stopwatch.StartNew();

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start '{processStartInfo.FileName}'.");
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task<TimeSpan>? processorTimeTask = captureProcessResources
                ? ProcessMeasurement.WaitForExitAndSampleTotalProcessorTimeAsync(process)
                : null;
            Task exitTask = processorTimeTask ?? process.WaitForExitAsync();
            long peakWorkingSetBytes = 0;
            while (captureProcessResources && !exitTask.IsCompleted)
            {
                peakWorkingSetBytes = UpdatePeakWorkingSet(process, peakWorkingSetBytes);
                await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromMilliseconds(10)));
            }

            await exitTask;
            await Task.WhenAll(stdoutTask, stderrTask);
            stopwatch.Stop();
            TimeSpan? processorTime = processorTimeTask is null ? null : await processorTimeTask;

            string standardOutput = await stdoutTask;
            string standardError = await stderrTask;
            TestRunCounts counts = ParseAndValidateResult(process.ExitCode, standardOutput, standardError, payload.Project.ExpectedTestCount);

            if (!isWarmup)
            {
                samples.Add(new(
                    Index: samples.Count + 1,
                    ElapsedMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    ProcessorMilliseconds: processorTime?.TotalMilliseconds,
                    PeakWorkingSetBytes: captureProcessResources ? peakWorkingSetBytes : null,
                    TotalTests: counts.Total,
                    PassedTests: counts.Passed,
                    FailedTests: counts.Failed,
                    SkippedTests: counts.Skipped));
            }
        }

        double[] elapsed = [.. samples.Select(sample => sample.ElapsedMilliseconds).OrderBy(value => value)];
        double[] processor = captureProcessResources
            ? [.. samples.Select(sample => sample.ProcessorMilliseconds!.Value).OrderBy(value => value)]
            : [];
        string pipeline = (string)context.Properties["PipelineName"];
        var report = new ProcessBenchmarkReport(
            SchemaVersion: 2,
            Pipeline: pipeline,
            Scenario: payload.Project.AssetName,
            ExecutionKind: executionKind,
            ProcessResourceScope: processResourceScope,
            TestPlatform: payload.Project.TestPlatform.ToString(),
            SourceGenerationMode: payload.Project.SourceGenerationMode.ToString(),
            TargetFramework: payload.Project.Tfms.Single(),
            Configuration: payload.BuildConfiguration.ToString(),
            Command: $"{processStartInfo.FileName} {processStartInfo.Arguments.Trim()}",
            ExpectedTestCount: payload.Project.ExpectedTestCount,
            RunnerRuntimeVersion: Environment.Version.ToString(),
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount: Environment.ProcessorCount,
            TotalAvailableMemoryBytes: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            CiImage: Environment.GetEnvironmentVariable("ImageOS"),
            CiImageVersion: Environment.GetEnvironmentVariable("ImageVersion"),
            Commit: Environment.GetEnvironmentVariable("GITHUB_SHA"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Summary: new(
                MedianElapsedMilliseconds: Percentile(elapsed, 0.5),
                LowerQuartileElapsedMilliseconds: Percentile(elapsed, 0.25),
                UpperQuartileElapsedMilliseconds: Percentile(elapsed, 0.75),
                MedianProcessorMilliseconds: captureProcessResources ? Percentile(processor, 0.5) : null),
            Samples: samples);

        string reportJson = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(payload.ResultFilePath, reportJson);

        string stableResultDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Results", pipeline);
        Directory.CreateDirectory(stableResultDirectory);
        await File.WriteAllTextAsync(Path.Combine(stableResultDirectory, "Result.json"), reportJson);
    }

    private static TestRunCounts ParseAndValidateResult(int exitCode, string standardOutput, string standardError, int expectedTestCount)
    {
        if (exitCode != 0)
        {
            throw CreateInvalidRunException($"The benchmark process exited with code {exitCode}.", standardOutput, standardError);
        }

        Match match = MtpSummaryRegex.Match(standardOutput);
        if (!match.Success)
        {
            match = VSTestSummaryRegex.Match(standardOutput);
        }

        if (!match.Success)
        {
            throw CreateInvalidRunException("The benchmark process did not emit a recognized test summary.", standardOutput, standardError);
        }

        var counts = new TestRunCounts(
            Total: int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture),
            Passed: int.Parse(match.Groups["passed"].Value, CultureInfo.InvariantCulture),
            Failed: int.Parse(match.Groups["failed"].Value, CultureInfo.InvariantCulture),
            Skipped: int.Parse(match.Groups["skipped"].Value, CultureInfo.InvariantCulture));

        return counts.Total == expectedTestCount && counts.Passed == expectedTestCount && counts.Failed == 0 && counts.Skipped == 0
            ? counts
            : throw CreateInvalidRunException(
                $"Expected {expectedTestCount} passing tests but observed {counts.Total} total, " +
                $"{counts.Passed} passed, {counts.Failed} failed, and {counts.Skipped} skipped.",
                standardOutput,
                standardError);
    }

    private static InvalidOperationException CreateInvalidRunException(string message, string standardOutput, string standardError)
        => new(
            $"{message}{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{standardError}");

    private static long UpdatePeakWorkingSet(Process process, long currentPeak)
    {
        try
        {
            process.Refresh();
            return Math.Max(currentPeak, process.PeakWorkingSet64);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return currentPeak;
        }
        catch (Win32Exception) when (process.HasExited)
        {
            return currentPeak;
        }
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        double position = (sortedValues.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        double fraction = position - lower;

        return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * fraction);
    }

    private sealed record ProcessBenchmarkReport(
        int SchemaVersion,
        string Pipeline,
        string Scenario,
        string ExecutionKind,
        string ProcessResourceScope,
        string TestPlatform,
        string SourceGenerationMode,
        string TargetFramework,
        string Configuration,
        string Command,
        int ExpectedTestCount,
        string RunnerRuntimeVersion,
        string OperatingSystem,
        string ProcessArchitecture,
        int ProcessorCount,
        long TotalAvailableMemoryBytes,
        string? CiImage,
        string? CiImageVersion,
        string? Commit,
        DateTimeOffset TimestampUtc,
        ProcessBenchmarkSummary Summary,
        IReadOnlyList<ProcessBenchmarkSample> Samples);

    private sealed record ProcessBenchmarkSummary(
        double MedianElapsedMilliseconds,
        double LowerQuartileElapsedMilliseconds,
        double UpperQuartileElapsedMilliseconds,
        double? MedianProcessorMilliseconds);

    private sealed record ProcessBenchmarkSample(
        int Index,
        double ElapsedMilliseconds,
        double? ProcessorMilliseconds,
        long? PeakWorkingSetBytes,
        int TotalTests,
        int PassedTests,
        int FailedTests,
        int SkippedTests);

    private sealed record TestRunCounts(int Total, int Passed, int Failed, int Skipped);
}
