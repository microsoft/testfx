// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TerminalTestReporterOptions
{
    /// <summary>
    /// Gets a value indicating whether we should show passed tests.
    /// </summary>
    public Func<bool> ShowPassedTests { get; init; } = () => true;

    /// <summary>
    /// Gets minimum amount of tests to run.
    /// </summary>
    public int MinimumExpectedTests { get; init; }

    /// <summary>
    /// Gets the policy that controls whether an all-skipped run is treated as a "zero tests ran" failure.
    /// </summary>
    public CommandLine.ZeroTestsPolicy ZeroTestsPolicy { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should write the progress periodically to screen. When ANSI is allowed we update the progress as often as we can.
    /// When ANSI is not allowed we never have progress.
    /// This is a callback to nullable bool, because we don't know if we are running as test host controller until after we setup the console. So we should be polling for the value, until we get non-null boolean
    /// and then cache that value.
    /// </summary>
    public Func<bool?> ShowProgress { get; init; } = () => true;

    /// <summary>
    /// Gets a value indicating whether the active tests should be visible when the progress is shown.
    /// </summary>
    public bool ShowActiveTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether per-assembly summary lines (with the compact pass/fail/skip counts) should be
    /// rendered. Used by the <c>dotnet test</c> orchestrator which runs multiple assemblies; the in-process host
    /// leaves this off.
    /// </summary>
    public bool ShowAssembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether, when <see cref="ShowAssembly"/> is set, the per-assembly summary line is
    /// printed mid-stream as each assembly completes.
    /// </summary>
    public bool ShowAssemblyStartAndComplete { get; init; }

    /// <summary>
    /// Gets a value indicating the ANSI mode.
    /// </summary>
    public AnsiMode AnsiMode { get; init; }

    /// <summary>
    /// Gets a value indicating when to show standard output.
    /// </summary>
    public OutputShowMode ShowStdout { get; init; } = OutputShowMode.All;

    /// <summary>
    /// Gets a value indicating when to show standard error output.
    /// </summary>
    public OutputShowMode ShowStderr { get; init; } = OutputShowMode.All;

    /// <summary>
    /// Gets the silence threshold for the non-cursor heartbeat renderer (E1). When no test completes for this
    /// duration, a single heartbeat line is emitted. <see cref="TimeSpan.Zero"/> disables the silence heartbeat.
    /// </summary>
    public TimeSpan HeartbeatSilenceThreshold { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the per-test threshold for the non-cursor heartbeat renderer (E2). When a single test exceeds this
    /// duration, a single "[slow]" line is emitted (with exponential backoff). <see cref="TimeSpan.Zero"/>
    /// disables slow-test surfacing.
    /// </summary>
    public TimeSpan SlowTestThreshold { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the number of slowest tests to list in the run summary. When greater than zero, a "Slowest tests"
    /// section ranking the longest-running tests (by their reported execution duration, which excludes fixture
    /// time) is appended to the summary. Zero (the default) disables the section.
    /// </summary>
    public int SlowestTestsCount { get; init; }
}

[Embedded]
internal enum OutputShowMode
{
    /// <summary>
    /// Always show the output.
    /// </summary>
    All,

    /// <summary>
    /// Show the output only for failed tests.
    /// </summary>
    Failed,

    /// <summary>
    /// Never show the output.
    /// </summary>
    None,
}

[Embedded]
internal enum AnsiMode
{
    /// <summary>
    /// Disable ANSI escape codes.
    /// </summary>
    NoAnsi,

    /// <summary>
    /// Use simplified ANSI renderer, which colors output, but does not move cursor.
    /// This is used in compatible CI environments.
    /// </summary>
    SimpleAnsi,

    /// <summary>
    /// Enable ANSI escape codes, including cursor movement, when the capabilities of the console allow it.
    /// </summary>
    AnsiIfPossible,

    /// <summary>
    /// Force ANSI escape codes, regardless of the capabilities of the console.
    /// This is needed only for testing.
    /// </summary>
    ForceAnsi,
}
