// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device.Android;

/// <summary>
/// Runs tests on Android devices.
/// </summary>
public sealed class AndroidDeviceTestRunner : IDeviceTestRunner
{
    private readonly AdbClient _adbClient;

    public AndroidDeviceTestRunner()
    {
        _adbClient = new AdbClient();
    }

    /// <inheritdoc/>
    public async Task<TestRunResult> RunTestsAsync(DeviceInfo device, TestRunOptions options, CancellationToken cancellationToken)
    {
        // Launch the test application
        string launchArgs = $"-s {device.Id} shell am start -n {options.AppId}/.MainActivity";

        if (options.TestFilter is not null)
        {
            launchArgs += $" --es TestFilter \"{options.TestFilter}\"";
        }

        if (options.CollectCoverage)
        {
            launchArgs += " --ez CollectCoverage true";
        }

        AdbResult launchResult = await _adbClient.ExecuteAsync(launchArgs, cancellationToken);
        if (!launchResult.Success)
        {
            return new TestRunResult(false, 1, launchResult.Output, $"Failed to launch app: {launchResult.Error}");
        }

        // Wait for app to complete (poll for process or wait for timeout)
        // For MVP, we'll use a simple delay and check logs
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        // Get logcat output to capture test results
        string logcatArgs = $"-s {device.Id} logcat -d -s TestResults:* TestPlatform:*";
        AdbResult logcatResult = await _adbClient.ExecuteAsync(logcatArgs, cancellationToken);

        // Parse exit code from logcat or use 0 for success
        int exitCode = logcatResult.Output.Contains("Test run failed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        return new TestRunResult(
            exitCode == 0,
            exitCode,
            logcatResult.Output,
            exitCode == 0 ? "Tests completed successfully" : "Tests failed");
    }
}
