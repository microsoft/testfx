// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device;

/// <summary>
/// Runs tests on a device.
/// </summary>
public interface IDeviceTestRunner
{
    /// <summary>
    /// Runs tests on the specified device.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="options">Test run options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test run result.</returns>
    Task<TestRunResult> RunTestsAsync(DeviceInfo device, TestRunOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Options for test execution.
/// </summary>
/// <param name="AppId">Application identifier.</param>
/// <param name="TestFilter">Optional test filter expression.</param>
/// <param name="Timeout">Test execution timeout.</param>
/// <param name="CollectCoverage">Whether to collect code coverage.</param>
public record TestRunOptions(
    string AppId,
    string? TestFilter = null,
    TimeSpan Timeout = default,
    bool CollectCoverage = false);

/// <summary>
/// Result of a test run.
/// </summary>
/// <param name="Success">Whether the test run completed successfully.</param>
/// <param name="ExitCode">Exit code of the test application.</param>
/// <param name="Output">Console output from the test run.</param>
/// <param name="Message">Status or error message.</param>
public record TestRunResult(
    bool Success,
    int ExitCode,
    string Output,
    string Message);
