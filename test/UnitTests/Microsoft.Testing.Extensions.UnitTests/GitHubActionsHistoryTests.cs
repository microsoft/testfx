// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.Json;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

using GitHubCiRunSummaryHistoryTest = ghactions::Microsoft.Testing.Extensions.CiRunSummaryHistoryTest;
using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ReadAsync_ReturnsEmptySnapshot_WhenFileDoesNotExist()
    {
        GitHubActionsHistorySnapshot snapshot = await GitHubActionsHistoryStore.ReadAsync(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json"),
            Now.AddDays(-30),
            CancellationToken.None);

        Assert.AreEqual(GitHubActionsHistoryStore.SchemaVersion, snapshot.SchemaVersion);
        Assert.IsEmpty(snapshot.Samples);
    }

    [TestMethod]
    public void Merge_KeepsLatestBoundedSamplesPerTest()
    {
        GitHubActionsHistorySample[] samples =
        [
            .. Enumerable.Range(0, GitHubActionsHistoryStore.MaxSamplesPerTest + 1)
                .Select(index =>
                {
                    GitHubActionsHistorySample sample = CreateSample(
                        "Tests.Flaky",
                        index % 2 == 0 ? GitHubActionsHistoryOutcome.Passed : GitHubActionsHistoryOutcome.Failed,
                        Now.AddMinutes(index));
                    sample.RunId = index.ToString(CultureInfo.InvariantCulture);
                    return sample;
                }),
        ];

        GitHubActionsHistorySnapshot merged = GitHubActionsHistoryStore.Merge(
            new GitHubActionsHistorySnapshot(),
            samples,
            Now);

        Assert.HasCount(GitHubActionsHistoryStore.MaxSamplesPerTest, merged.Samples);
        Assert.AreEqual(Now.AddMinutes(1), merged.Samples[0].TimestampUtc);
        Assert.AreEqual(Now.AddMinutes(GitHubActionsHistoryStore.MaxSamplesPerTest), merged.Samples[^1].TimestampUtc);
        Assert.AreSequenceEqual(
            Enumerable.Range(1, GitHubActionsHistoryStore.MaxSamplesPerTest)
                .Select(index => Now.AddMinutes(index)),
            merged.Samples.Select(static sample => sample.TimestampUtc));
    }

    [TestMethod]
    public void AggregateStats_CountsPassesAndFailuresButNotSkips()
    {
        GitHubActionsHistorySample passed = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Passed, Now);
        passed.RunId = "1";
        GitHubActionsHistorySample failed = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Failed, Now);
        failed.RunId = "2";
        GitHubActionsHistorySample skipped = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Skipped, Now);
        skipped.RunId = "3";
        var snapshot = new GitHubActionsHistorySnapshot
        {
            Samples = [passed, failed, skipped],
        };

        IReadOnlyDictionary<
            (string TestId, string FullyQualifiedName, string DisplayName),
            GitHubActionsHistoryStats> statsByTest =
            GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope());

        Assert.IsTrue(statsByTest.TryGetValue(
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky"),
            out GitHubActionsHistoryStats stats));
        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(1, stats.FailCount);
        Assert.AreEqual(2, stats.TotalCount);
        Assert.AreEqual(0.5, stats.FailureRate);
    }

    [TestMethod]
    public void Merge_KeepsLatestBoundedSamplesOverall()
    {
        GitHubActionsHistorySample[] samples =
        [
            .. Enumerable.Range(0, GitHubActionsHistoryStore.MaxTotalSamples + 1)
                .Select(index => CreateSample($"Tests.Test{index}", GitHubActionsHistoryOutcome.Passed, Now.AddTicks(index))),
        ];

        GitHubActionsHistorySnapshot merged = GitHubActionsHistoryStore.Merge(
            new GitHubActionsHistorySnapshot(),
            samples,
            Now);

        Assert.HasCount(GitHubActionsHistoryStore.MaxTotalSamples, merged.Samples);
        Assert.AreEqual(Now.AddTicks(1), merged.Samples[0].TimestampUtc);
    }

    [TestMethod]
    public void Merge_DeduplicatesRepeatedPostProcessingOfSameRun()
    {
        GitHubActionsHistorySample existingSample = CreateSample(
            "Tests.Flaky",
            GitHubActionsHistoryOutcome.Passed,
            Now.AddMinutes(-1));
        GitHubActionsHistorySample repeatedSample = CreateSample(
            "Tests.Flaky",
            GitHubActionsHistoryOutcome.Passed,
            Now);

        GitHubActionsHistorySnapshot merged = GitHubActionsHistoryStore.Merge(
            new GitHubActionsHistorySnapshot { Samples = [existingSample] },
            [repeatedSample],
            Now);

        Assert.HasCount(1, merged.Samples);
        Assert.AreEqual(Now, merged.Samples[0].TimestampUtc);
    }

    [TestMethod]
    public void Merge_DeduplicatesArchitectureAndRunnerOsCasingVariants()
    {
        GitHubActionsHistorySample lowerCase = CreateSample(
            "Tests.Flaky",
            GitHubActionsHistoryOutcome.Passed,
            Now.AddMinutes(-1));
        lowerCase.Architecture = "x64";
        lowerCase.RunnerOs = "windows";
        GitHubActionsHistorySample upperCase = CreateSample(
            "Tests.Flaky",
            GitHubActionsHistoryOutcome.Passed,
            Now);
        upperCase.Architecture = "X64";
        upperCase.RunnerOs = "WINDOWS";

        GitHubActionsHistorySnapshot merged = GitHubActionsHistoryStore.Merge(
            new GitHubActionsHistorySnapshot { Samples = [lowerCase] },
            [upperCase],
            Now);

        Assert.HasCount(1, merged.Samples);
        Assert.AreEqual(Now, merged.Samples[0].TimestampUtc);
        GitHubActionsHistoryStats stats = GitHubActionsHistoryStore.AggregateStats(merged, CreateScope())[
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky")];
        Assert.AreEqual(1, stats.DurationSampleCount);
    }

    [TestMethod]
    public void AggregateStats_DoesNotMixMatchingNamesFromOtherScopes()
    {
        GitHubActionsHistorySample otherAssembly = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Failed, Now);
        otherAssembly.AssemblyName = "OtherTests";
        var snapshot = new GitHubActionsHistorySnapshot
        {
            Samples =
            [
                CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Passed, Now),
                otherAssembly,
            ],
        };

        IReadOnlyDictionary<
            (string TestId, string FullyQualifiedName, string DisplayName),
            GitHubActionsHistoryStats> statsByTest =
            GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope());

        Assert.IsTrue(statsByTest.TryGetValue(
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky"),
            out GitHubActionsHistoryStats stats));
        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(0, stats.FailCount);
    }

    [TestMethod]
    public void AggregateStats_RequiresMatchingTargetFrameworkArchitectureAndRunnerOs()
    {
        GitHubActionsHistorySample matching = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Passed, Now);
        GitHubActionsHistorySample otherFramework = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Failed, Now);
        otherFramework.TargetFramework = "net9.0";
        GitHubActionsHistorySample otherArchitecture = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Failed, Now);
        otherArchitecture.Architecture = "arm64";
        GitHubActionsHistorySample otherRunner = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Failed, Now);
        otherRunner.RunnerOs = "Linux";
        var snapshot = new GitHubActionsHistorySnapshot
        {
            Samples = [matching, otherFramework, otherArchitecture, otherRunner],
        };

        GitHubActionsHistoryStats stats = GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope())[
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky")];

        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(0, stats.FailCount);
    }

    [TestMethod]
    public void AggregateStats_MatchesArchitectureAndRunnerOsCaseInsensitively()
    {
        GitHubActionsHistorySample sample = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Passed, Now);
        sample.Architecture = "X64";
        sample.RunnerOs = "WINDOWS";
        var snapshot = new GitHubActionsHistorySnapshot { Samples = [sample] };

        GitHubActionsHistoryStats stats = GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope())[
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky")];

        Assert.AreEqual(1, stats.PassCount);
    }

    [TestMethod]
    public void AggregateStats_CountsFlakyRuns()
    {
        GitHubActionsHistorySample sample = CreateSample("Tests.Flaky", GitHubActionsHistoryOutcome.Passed, Now);
        sample.IsFlaky = true;
        var snapshot = new GitHubActionsHistorySnapshot { Samples = [sample] };

        GitHubActionsHistoryStats stats = GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope())[
            ("Tests.Flaky", "Tests.Flaky", "Tests.Flaky")];

        Assert.AreEqual(1, stats.PassCount);
        Assert.AreEqual(1, stats.FlakyCount);
    }

    [TestMethod]
    public void AggregateStats_DoesNotMixParameterizedRowsWithSameFullyQualifiedName()
    {
        GitHubActionsHistorySample firstRow = CreateSample(
            "Tests.Parameterized",
            GitHubActionsHistoryOutcome.Passed,
            Now);
        firstRow.TestId = "row-1";
        firstRow.DisplayName = "Parameterized(1)";
        GitHubActionsHistorySample secondRow = CreateSample(
            "Tests.Parameterized",
            GitHubActionsHistoryOutcome.Failed,
            Now);
        secondRow.TestId = "row-2";
        secondRow.DisplayName = "Parameterized(2)";
        var snapshot = new GitHubActionsHistorySnapshot { Samples = [firstRow, secondRow] };

        IReadOnlyDictionary<
            (string TestId, string FullyQualifiedName, string DisplayName),
            GitHubActionsHistoryStats> stats = GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope());

        Assert.AreEqual(1, stats[("row-1", "Tests.Parameterized", "Parameterized(1)")].PassCount);
        Assert.AreEqual(0, stats[("row-1", "Tests.Parameterized", "Parameterized(1)")].FailCount);
        Assert.AreEqual(0, stats[("row-2", "Tests.Parameterized", "Parameterized(2)")].PassCount);
        Assert.AreEqual(1, stats[("row-2", "Tests.Parameterized", "Parameterized(2)")].FailCount);
    }

    [TestMethod]
    public void AggregateStats_ComputesDurationPercentiles()
    {
        GitHubActionsHistorySample[] samples =
        [
            .. Enumerable.Range(1, 20).Select(index =>
            {
                GitHubActionsHistorySample sample = CreateSample(
                    "Tests.Duration",
                    GitHubActionsHistoryOutcome.Passed,
                    Now.AddMinutes(index));
                sample.RunId = index.ToString(CultureInfo.InvariantCulture);
                sample.DurationTicks = TimeSpan.FromSeconds(index).Ticks;
                return sample;
            }),
        ];
        var snapshot = new GitHubActionsHistorySnapshot { Samples = samples };

        GitHubActionsHistoryStats stats = GitHubActionsHistoryStore.AggregateStats(snapshot, CreateScope())[
            ("Tests.Duration", "Tests.Duration", "Tests.Duration")];

        Assert.AreEqual(TimeSpan.FromSeconds(19), stats.P95Duration);
        Assert.AreEqual(TimeSpan.FromSeconds(20), stats.P99Duration);
        Assert.AreEqual(20, stats.DurationSampleCount);
    }

    [TestMethod]
    public async Task ReadAsync_ObservesCancellationBeforeWaitingForLockAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-cancelled-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """{"schemaVersion":1,"samples":[]}""");
            using FileStream lockStream = new(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var cancellationTokenSource = new CancellationTokenSource();
#pragma warning disable VSTHRD103 // CancelAsync is unavailable on all target frameworks.
            cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), cancellationTokenSource.Token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_RejectsOversizedFileBeforeDeserializationAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-oversized-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            using (FileStream stream = File.Create(path))
            {
                stream.SetLength(GitHubActionsHistoryStore.MaxSnapshotBytes + 1);
            }

            await Assert.ThrowsExactlyAsync<FormatException>(
                () => GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_RejectsSnapshotAboveSampleLimitAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-too-many-samples-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            var snapshot = new GitHubActionsHistorySnapshot
            {
                Samples =
                [
                    .. Enumerable.Range(0, GitHubActionsHistoryStore.MaxTotalSamples + 1)
                        .Select(index => CreateSample($"Tests.Test{index}", GitHubActionsHistoryOutcome.Passed, Now)),
                ],
            };
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(snapshot, GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot));

            await Assert.ThrowsExactlyAsync<FormatException>(
                () => GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_RejectsSnapshotAbovePerTestSampleLimitAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-too-many-test-samples-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            var snapshot = new GitHubActionsHistorySnapshot
            {
                Samples =
                [
                    .. Enumerable.Range(0, GitHubActionsHistoryStore.MaxSamplesPerTest + 1)
                        .Select(index =>
                        {
                            GitHubActionsHistorySample sample = CreateSample(
                                "Tests.Crowded",
                                GitHubActionsHistoryOutcome.Passed,
                                Now);
                            sample.RunId = index.ToString(CultureInfo.InvariantCulture);
                            sample.Architecture = index % 2 == 0 ? "x64" : "X64";
                            sample.RunnerOs = index % 2 == 0 ? "Windows" : "WINDOWS";
                            return sample;
                        }),
                ],
            };
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(snapshot, GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot));

            await Assert.ThrowsExactlyAsync<FormatException>(
                () => GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_RoundTripsAndPrunesExpiredSamplesAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        try
        {
            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [
                    CreateSample("Tests.Old", GitHubActionsHistoryOutcome.Failed, Now.AddDays(-31)),
                    CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now),
                ],
                CancellationToken.None);

            GitHubActionsHistorySnapshot snapshot = await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);

            Assert.HasCount(1, snapshot.Samples);
            Assert.AreEqual("Tests.Current", snapshot.Samples[0].FullyQualifiedName);
            Assert.AreEqual(Now, snapshot.GeneratedAtUtc);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_ReplacesInvalidSnapshotAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-invalid-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """{"schemaVersion":0,"samples":[]}""");

            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now)],
                CancellationToken.None);

            GitHubActionsHistorySnapshot snapshot = await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);
            Assert.HasCount(1, snapshot.Samples);
            Assert.AreEqual(GitHubActionsHistoryStore.SchemaVersion, snapshot.SchemaVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_DoesNotReplaceNewerSnapshotAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-newer-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        const string NewerSnapshot = """{"schemaVersion":999,"samples":[]}""";
        try
        {
            File.WriteAllText(path, NewerSnapshot);

            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                () => GitHubActionsHistoryStore.WriteMergedAsync(
                    path,
                    historyWindowInDays: 30,
                    Now,
                    [CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now)],
                    CancellationToken.None));

            Assert.AreEqual(NewerSnapshot, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_ReplacesTruncatedSnapshotAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-truncated-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """{"schemaVersion":1,"samples":[""");

            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now)],
                CancellationToken.None);

            GitHubActionsHistorySnapshot snapshot = await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);
            Assert.HasCount(1, snapshot.Samples);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_ReplacesSnapshotContainingNullSampleAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-null-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """{"schemaVersion":1,"samples":[null]}""");

            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now)],
                CancellationToken.None);

            GitHubActionsHistorySnapshot snapshot =
                await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);
            Assert.AreEqual("Tests.Current", snapshot.Samples.Single().FullyQualifiedName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_ReplacesSnapshotContainingNullScopeFieldsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-null-scope-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                path,
                $$"""{"schemaVersion":1,"samples":[{"runAttempt":1,"timestampUtc":"{{Now:O}}","assemblyName":"Tests","targetFramework":"net8.0","architecture":null,"runnerOs":null,"testId":"Tests.Old","fullyQualifiedName":"Tests.Old","displayName":"Tests.Old","outcome":"passed","durationTicks":1,"isFlaky":false}]}""");

            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [CreateSample("Tests.Current", GitHubActionsHistoryOutcome.Passed, Now)],
                CancellationToken.None);

            GitHubActionsHistorySnapshot snapshot =
                await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);
            Assert.AreEqual("Tests.Current", snapshot.Samples.Single().FullyQualifiedName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteMergedAsync_PreservesPriorSnapshotWhenNewSnapshotExceedsByteLimitAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-write-limit-history-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        try
        {
            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [CreateSample("Tests.Existing", GitHubActionsHistoryOutcome.Passed, Now.AddMinutes(-1))],
                CancellationToken.None);
            string priorSnapshot = File.ReadAllText(path);
            string longPrefix = new('x', 600);
            GitHubActionsHistorySample[] oversizedSamples =
            [
                .. Enumerable.Range(0, GitHubActionsHistoryStore.MaxTotalSamples)
                    .Select(index => CreateSample(
                        $"Tests.{longPrefix}{index.ToString(CultureInfo.InvariantCulture)}",
                        GitHubActionsHistoryOutcome.Passed,
                        Now)),
            ];

            await Assert.ThrowsExactlyAsync<FormatException>(
                () => GitHubActionsHistoryStore.WriteMergedAsync(
                    path,
                    historyWindowInDays: 30,
                    Now,
                    oversizedSamples,
                    CancellationToken.None));

            Assert.AreEqual(priorSnapshot, File.ReadAllText(path));
            GitHubActionsHistorySnapshot snapshot =
                await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-30), CancellationToken.None);
            Assert.AreEqual("Tests.Existing", snapshot.Samples.Single().FullyQualifiedName);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task HistoryService_LoadsPriorStatsAndWritesCurrentRunMetadataAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"github-history-service-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "history.json");
        try
        {
            GitHubActionsHistorySample priorRun = CreateSample(
                "Tests.Flaky",
                GitHubActionsHistoryOutcome.Failed,
                Now.AddDays(-1));
            GitHubActionsHistorySample earlierAttempt = CreateSample(
                "Tests.Flaky",
                GitHubActionsHistoryOutcome.Passed,
                Now.AddHours(-1));
            earlierAttempt.RunId = "123";
            earlierAttempt.RunAttempt = 1;
            GitHubActionsHistorySample currentAttempt = CreateSample(
                "Tests.Flaky",
                GitHubActionsHistoryOutcome.Failed,
                Now);
            currentAttempt.RunId = "123";
            currentAttempt.RunAttempt = 2;
            await GitHubActionsHistoryStore.WriteMergedAsync(
                path,
                historyWindowInDays: 30,
                Now,
                [priorRun, earlierAttempt, currentAttempt],
                CancellationToken.None);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns("true");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_RUN_ID")).Returns("123");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")).Returns("2");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_SHA")).Returns("deadbeef");
            environment.Setup(item => item.GetEnvironmentVariable("GITHUB_REF_NAME")).Returns("main");
            environment.Setup(item => item.GetEnvironmentVariable("RUNNER_OS")).Returns("Windows");
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            GitHubActionsHistoryService service = new(
                new TestCommandLineOptions(new Dictionary<string, string[]>
                {
                    [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
                    [GitHubActionsCommandLineOptions.GitHubActionsHistory] = [path],
                    [GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow] = ["14"],
                }),
                environment.Object,
                new TestClock(),
                loggerFactory.Object,
                CreateScope());

            await service.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());

            Assert.IsTrue(service.TryGetStats(
                "Tests.Flaky",
                "Tests.Flaky",
                "Tests.Flaky",
                out GitHubActionsHistoryStats stats));
            Assert.AreEqual(1, stats.FailCount);
            Assert.AreEqual(1, stats.PassCount);
            Assert.AreEqual(14, service.HistoryWindowInDays);

            await service.WriteAsync(
                [
                    new GitHubCiRunSummaryModule
                    {
                        AssemblyName = "Tests",
                        TargetFramework = "net8.0",
                        Architecture = "x64",
                        GitHubActionsHistoryPath = path,
                        GitHubActionsHistoryWindowInDays = 14,
                        HistoryTests =
                        [
                            new GitHubCiRunSummaryHistoryTest
                            {
                                TestId = "uid",
                                FullyQualifiedName = "Tests.Current",
                                DisplayName = "Current",
                                Outcome = GitHubActionsHistoryOutcome.Passed,
                                DurationTicks = TimeSpan.FromMilliseconds(25).Ticks,
                            },
                        ],
                    },
                ],
                CancellationToken.None);

            GitHubActionsHistorySample current = (await GitHubActionsHistoryStore.ReadAsync(path, Now.AddDays(-14), CancellationToken.None))
                .Samples.Single(sample => sample.FullyQualifiedName == "Tests.Current");
            Assert.AreEqual("123", current.RunId);
            Assert.AreEqual(2, current.RunAttempt);
            Assert.AreEqual("deadbeef", current.CommitSha);
            Assert.AreEqual("main", current.RefName);
            Assert.AreEqual("Windows", current.RunnerOs);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static GitHubActionsHistorySample CreateSample(string fullyQualifiedName, string outcome, DateTimeOffset timestamp)
        => new()
        {
            RunId = "42",
            RunAttempt = 1,
            TimestampUtc = timestamp,
            CommitSha = "abcdef",
            RefName = "main",
            RunnerOs = "Windows",
            AssemblyName = "Tests",
            TargetFramework = "net8.0",
            Architecture = "x64",
            TestId = fullyQualifiedName,
            FullyQualifiedName = fullyQualifiedName,
            DisplayName = fullyQualifiedName,
            Outcome = outcome,
            DurationTicks = TimeSpan.FromMilliseconds(10).Ticks,
        };

    private static GitHubActionsHistoryScope CreateScope()
        => new("Tests", "net8.0", "x64", "Windows");

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
