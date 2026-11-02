// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class StepSummaryTransactionBehaviorTests
{
    [TestMethod]
    [DataRow(0, DisplayName = "SummaryMeasure_IoFailure")]
    [DataRow(1, DisplayName = "SummaryMeasure_AccessFailure")]
    [DataRow(2, DisplayName = "SummaryMeasure_UnsupportedStream")]
    public void Measure_FailuresAreBestEffortAndTraced(int failure)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "preserved");
        fixture.BeforeOpen = (_, _, _, _) => throw failure switch
        {
            0 => new IOException("measurement denied"),
            1 => new UnauthorizedAccessException("measurement denied"),
            _ => new NotSupportedException("measurement denied"),
        };
        StepSummaryWriter writer = Writer(fixture, path);

        Assert.IsNull(writer.GetSummaryLength());
        Assert.AreEqual(0L, writer.GetSummaryLengthExcludingSection("run"));

        Assert.AreEqual("preserved", fixture.ReadText(path));
        Assert.HasCount(2, fixture.Logs);
        Assert.IsTrue(fixture.Logs.All(entry => entry.Level == LogLevel.Trace && entry.Message == $"Could not measure '{path}': measurement denied"));
        Assert.IsEmpty(fixture.Replacements);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SummaryMeasure_CompleteOwnSection")]
    [DataRow(1, DisplayName = "SummaryMeasure_NoOwnSection")]
    [DataRow(2, DisplayName = "SummaryMeasure_IncompleteOwnSection")]
    [DataRow(3, DisplayName = "SummaryMeasure_FencedForgedSection")]
    public void Measure_ExcludesOnlyCompleteUnfencedOwnSection(int partition)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        string own = Section("run", "old");
        string content = partition switch
        {
            0 => "é\n" + own + "界\n",
            1 => "é\nordinary text\n界\n",
            2 => "é\n" + Start("run") + "\nmissing end\n界\n",
            _ => "é\n````text\n" + own + "````\n界\n",
        };
        fixture.PutText(path, content);
        StepSummaryWriter writer = Writer(fixture, path);

        long length = writer.GetSummaryLengthExcludingSection("run");

        Assert.AreEqual(Encoding.UTF8.GetByteCount(partition == 0 ? "é\n\n界\n" : content), length);
        Assert.AreEqual(Encoding.UTF8.GetByteCount(content), writer.GetSummaryLength());
        Assert.AreEqual(content, fixture.ReadText(path));
        Assert.IsEmpty(fixture.Replacements);
    }

    [TestMethod]
    public void Markers_CountOnlyWholeUnfencedProjectLines()
    {
        string marker = GitHubActionsSummaryReporter.ProjectSectionMarker;
        string summary = marker + "\r\nprefix " + marker + "\n````text\n" + marker
            + "\n```\n" + marker + "\n```` not-a-close\n" + marker + "\n````\n" + marker + "\n";

        Assert.AreEqual(2, StepSummaryWriter.CountProjectSections(summary));
        Assert.AreEqual(0, StepSummaryWriter.CountProjectSections("```text\n" + marker));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SummaryNotice_LegacyClosedHeader")]
    [DataRow(true, DisplayName = "SummaryNotice_UnterminatedLegacyHeader")]
    public async Task Notice_LegacyAndUnterminatedHeaderCannotHideStrongerLoss(bool unterminated)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        string marker = GitHubActionsSummaryReporter.TruncationNoticeMarker;
        string legacy = marker + "\r\nlegacy note\r\n"
            + (unterminated ? string.Empty : GitHubActionsSummaryReporter.TruncationNoticeEndMarker + "\r\n\r\n");
        string body = GitHubActionsSummaryReporter.ProjectSectionMarker + "\nforeign section\n";
        fixture.PutText(path, legacy + body);
        string stronger = GitHubActionsSummaryReporter.BuildTruncationNotice(1);
        StepSummaryWriter writer = Writer(fixture, path);

        Assert.AreEqual(1, StepSummaryWriter.GetLeadingNoticeStrength(legacy));
        Assert.IsTrue(await writer.UpsertStepSummaryWithRetryAsync("run", "new", CancellationToken.None, _ => stronger));

        string expected = stronger + (unterminated ? legacy : string.Empty) + body.TrimEnd() + "\n\n" + Section("run", "new");
        Assert.AreEqual(expected, fixture.ReadText(path));
        Assert.AreEqual(2, StepSummaryWriter.GetLeadingNoticeStrength(fixture.ReadText(path)));
        Assert.AreEqual(0, StepSummaryWriter.GetLeadingNoticeStrength("prefix\n" + marker));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SummaryUpsert_RetryLock")]
    [DataRow(true, DisplayName = "SummaryUpsert_RetryValidation")]
    public async Task Upsert_RetriesLockOrValidationContention(bool validation)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "foreign");
        bool failed = false;
        fixture.BeforeOpen = (opened, _, access, share) =>
        {
            bool selected = validation
                ? opened == path && access == FileAccess.Read && share != (FileShare.ReadWrite | FileShare.Delete)
                : opened == path + ".microsoft-testing-platform.lock";
            if (selected && !failed)
            {
                failed = true;
                throw new IOException("deterministic contention");
            }
        };

        bool written = await Writer(fixture, path, attempts: 2).UpsertStepSummaryWithRetryAsync("run", "new", CancellationToken.None);

        Assert.IsTrue(written);
        Assert.IsTrue(failed);
        Assert.AreEqual("foreign\n\n" + Section("run", "new"), fixture.ReadText(path));
        Assert.HasCount(2, fixture.Opens.Where(open => open.Path == path + ".microsoft-testing-platform.lock"));
        Assert.AreSequenceEqual([path], fixture.Replacements);
        Assert.DoesNotContain(file => file.EndsWith(".tmp", StringComparison.Ordinal), fixture.Files.Keys);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SummaryUpsert_PersistentInterferencePreservesForeignOutput")]
    [DataRow(true, DisplayName = "SummaryUpsert_TransientInterferenceRetriesSnapshot")]
    public async Task Upsert_StagingInterferenceRetriesOrPreservesForeignOutput(bool retry)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "base\n");
        int interferences = 0;
        fixture.BeforeOpen = (opened, mode, _, _) =>
        {
            if (mode == FileMode.CreateNew && opened.EndsWith(".tmp", StringComparison.Ordinal) && interferences++ == 0)
            {
                fixture.PutText(path, fixture.ReadText(path) + "foreign\n");
            }
        };
        StepSummaryWriter writer = Writer(fixture, path, retry ? 2 : 1);

        if (retry)
        {
            Assert.IsTrue(await writer.UpsertStepSummaryWithRetryAsync("run", "new", CancellationToken.None));
            Assert.AreEqual("base\nforeign\n\n" + Section("run", "new"), fixture.ReadText(path));
            Assert.AreSequenceEqual([path], fixture.Replacements);
        }
        else
        {
            IOException error = await Assert.ThrowsExactlyAsync<IOException>(() => writer.UpsertStepSummaryWithRetryAsync("run", "new", CancellationToken.None));
            Assert.AreEqual($"The GitHub step summary '{path}' kept changing while its replacement was staged.", error.Message);
            Assert.AreEqual("base\nforeign\n", fixture.ReadText(path));
            Assert.IsEmpty(fixture.Replacements);
        }

        Assert.DoesNotContain(file => file.EndsWith(".tmp", StringComparison.Ordinal), fixture.Files.Keys);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SummaryUpsert_SwapAndIoCleanupFailure")]
    [DataRow(1, DisplayName = "SummaryUpsert_SwapAndAccessCleanupFailure")]
    [DataRow(2, DisplayName = "SummaryUpsert_SwapFailure")]
    public async Task Upsert_ReplacementFailureAndCleanupDoNotDestroyOriginal(int cleanup)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "original content\n");
        var original = new IOException("swap unavailable");
        fixture.BeforeReplace = (_, _) => throw original;
        fixture.BeforeDelete = file =>
        {
            if (cleanup < 2 && file.EndsWith(".tmp", StringComparison.Ordinal))
            {
                throw cleanup == 0 ? new IOException("cleanup denied") : new UnauthorizedAccessException("cleanup denied");
            }
        };

        IOException error = await Assert.ThrowsExactlyAsync<IOException>(() => Writer(fixture, path).UpsertStepSummaryWithRetryAsync("run", "new", CancellationToken.None));

        Assert.AreSame(original, error);
        Assert.AreEqual("original content\n", fixture.ReadText(path));
        Assert.IsEmpty(fixture.Replacements);
        Assert.AreEqual(cleanup < 2, fixture.Files.Keys.Any(file => file.EndsWith(".tmp", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AppendNotice_RetriesOpenAndHandlesShortRead()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "abc");
        int opens = 0;
        fixture.FileSystem.Setup(value => value.NewFileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            .Returns(() => ++opens == 1
                ? throw new IOException("deterministic open contention")
                : new StreamFile(new DeclaredLengthStream("abc", 8)));
        fixture.FileSystem.Setup(value => value.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            .Returns(() => new StreamFile(new DeclaredLengthStream("abc", 8)));
        string notice = GitHubActionsSummaryReporter.BuildTruncationNotice(0);

        bool written = await Writer(fixture, path, attempts: 2).AppendStepSummaryWithLeadingNoticeAsync("tail", _ => notice, CancellationToken.None);

        Assert.IsTrue(written);
        Assert.AreEqual(2, opens);
        Assert.AreEqual(notice + "abctail", fixture.ReadText(path));
        Assert.DoesNotContain("\0", fixture.ReadText(path));
        Assert.AreSequenceEqual([path], fixture.Replacements);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SummaryAppend_FallbackAndIoCleanupFailure")]
    [DataRow(true, DisplayName = "SummaryAppend_FallbackAndAccessCleanupFailure")]
    public async Task AppendNotice_ExhaustedStagingRetriesFallBackToSafeAppend(bool accessFailure)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        string initial = GitHubActionsSummaryReporter.ProjectSectionMarker + "\noriginal\n";
        fixture.PutText(path, initial);
        int stages = 0;
        fixture.BeforeOpen = (opened, mode, _, _) =>
        {
            if (mode == FileMode.CreateNew && opened.EndsWith(".tmp", StringComparison.Ordinal))
            {
                stages++;
                fixture.PutText(path, fixture.ReadText(path) + "foreign\n");
            }
        };
        fixture.BeforeDelete = file =>
        {
            if (file.EndsWith(".tmp", StringComparison.Ordinal))
            {
                throw accessFailure ? new UnauthorizedAccessException("cleanup denied") : new IOException("cleanup denied");
            }
        };
        List<int> sectionCounts = [];

        bool written = await Writer(fixture, path).AppendStepSummaryWithLeadingNoticeAsync("tail\n", count =>
        {
            sectionCounts.Add(count);
            return GitHubActionsSummaryReporter.BuildTruncationNotice(count);
        }, CancellationToken.None);

        Assert.IsTrue(written);
        Assert.AreEqual(initial + "foreign\ntail\n", fixture.ReadText(path));
        Assert.AreEqual(1, stages);
        Assert.AreSequenceEqual([1, 1], sectionCounts);
        Assert.IsEmpty(fixture.Replacements);
    }

    [TestMethod]
    public async Task AppendNotice_ByteBudgetRefusesWithoutChangingExistingContent()
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "é");
        string notice = GitHubActionsSummaryReporter.BuildTruncationNotice(0);
        long budget = Encoding.UTF8.GetByteCount(notice + "é界") - 1;

        bool written = await Writer(fixture, path).AppendStepSummaryWithLeadingNoticeAsync("界", _ => notice, CancellationToken.None, budget);

        Assert.IsFalse(written);
        Assert.AreEqual("é", fixture.ReadText(path));
        Assert.IsEmpty(fixture.Replacements);
        Assert.DoesNotContain(file => file.EndsWith(".tmp", StringComparison.Ordinal), fixture.Files.Keys);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "SummaryUpsert_IncompleteSection")]
    [DataRow(true, DisplayName = "SummaryUpsert_FencedEndDoesNotCompleteSection")]
    public async Task Upsert_IncompleteSectionCannotBeRepairedByFencedEndMarker(bool forgedEnd)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        string original = "foreign\n" + Start("run") + "\nold body\n"
            + (forgedEnd ? "````text\n<!-- microsoft-testing-platform:github-actions:run:end -->\n````\n" : string.Empty);
        fixture.PutText(path, original);

        FormatException error = await Assert.ThrowsExactlyAsync<FormatException>(() =>
            Writer(fixture, path).UpsertStepSummaryWithRetryAsync("run", "new body", CancellationToken.None));

        Assert.AreEqual("The existing GitHub step summary contains an incomplete Microsoft Testing Platform summary section.", error.Message);
        Assert.AreEqual(original, fixture.ReadText(path));
        Assert.IsEmpty(fixture.Replacements);
        Assert.DoesNotContain(file => file.EndsWith(".tmp", StringComparison.Ordinal), fixture.Files.Keys);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "SummaryUpsert_CanceledBeforeLock")]
    [DataRow(1, DisplayName = "SummaryUpsert_CanceledDuringStaging")]
    [DataRow(2, DisplayName = "SummaryUpsert_CanceledDuringValidation")]
    public async Task Upsert_CancellationNeverCommitsStagedReplacement(int phase)
    {
        using var fixture = new CiReporterFixture();
        string path = Path.Combine(fixture.Root, "summary.md");
        fixture.PutText(path, "foreign content\n");
        using var cancellation = new CancellationTokenSource();
        if (phase == 0)
        {
            cancellation.Cancel();
        }

        fixture.BeforeOpen = (opened, _, access, share) =>
        {
            if ((phase == 1 && opened.EndsWith(".tmp", StringComparison.Ordinal))
                || (phase == 2 && opened == path && access == FileAccess.Read && share != (FileShare.ReadWrite | FileShare.Delete)))
            {
                cancellation.Cancel();
            }
        };

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Writer(fixture, path).UpsertStepSummaryWithRetryAsync("run", "new body", cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual("foreign content\n", fixture.ReadText(path));
        Assert.IsEmpty(fixture.Replacements);
        Assert.DoesNotContain(file => file.EndsWith(".tmp", StringComparison.Ordinal), fixture.Files.Keys);
        Assert.AreEqual(phase != 0, fixture.Files.ContainsKey(path + ".microsoft-testing-platform.lock"));
    }

    private static StepSummaryWriter Writer(CiReporterFixture fixture, string path, int attempts = 1)
        => new(fixture.FileSystem.Object, path, fixture.Logger.Object, attempts, TimeSpan.Zero);

    private static string Start(string id) => $"<!-- microsoft-testing-platform:github-actions:{id}:start -->";

    private static string Section(string id, string body)
        => Start(id) + "\n" + body + $"\n<!-- microsoft-testing-platform:github-actions:{id}:end -->\n";

    private sealed class DeclaredLengthStream : MemoryStream
    {
        private readonly long _declaredLength;

        public DeclaredLengthStream(string content, long length)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            Write(bytes, 0, bytes.Length);
            Position = 0;
            _declaredLength = length;
        }

        public override long Length => _declaredLength;
    }

    private sealed class StreamFile(Stream stream) : IFileStream
    {
        public Stream Stream { get; } = stream;

        public string Name => "owned-short-read";

        public void Dispose() => Stream.Dispose();

#if NETCOREAPP
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
#endif
    }
}
