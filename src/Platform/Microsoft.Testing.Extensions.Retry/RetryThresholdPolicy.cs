// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Policy;

/// <summary>
/// Evaluates the failure-threshold policy (--retry-failed-tests-max-percentage / --retry-failed-tests-max-tests)
/// against the first attempt's result and reports an explanation when the policy disables retrying.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal static class RetryThresholdPolicy
{
    /// <summary>
    /// Returns <see langword="true"/> when the number of failed tests exceeds the configured threshold, meaning
    /// the retry mechanism should be disabled. When the policy trips, an explanatory error is written to the
    /// output device.
    /// </summary>
    public static async Task<bool> EvaluateAsync(
        ICommandLineOptions commandLineOptions,
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        RetryFailedTestsPipeServer retryFailedTestsPipeServer,
        CancellationToken cancellationToken)
    {
        // The two threshold options intentionally use different units. The percentage option is a ratio over the
        // platform result counts, so its numerator must be failed results too. The count option is an absolute number
        // of failed tests, so folded data-driven rows sharing one uid count as one test. The options are mutually
        // exclusive, keeping each branch internally consistent.
        if (commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, out string[]? retryFailedTestsMaxPercentage))
        {
            double maxPercentage = double.Parse(retryFailedTestsMaxPercentage[0], CultureInfo.InvariantCulture);
            double maxFailedTestResults = maxPercentage / 100 * retryFailedTestsPipeServer.TotalTestRan;
            if (retryFailedTestsPipeServer.FailedTestResults <= maxFailedTestResults)
            {
                return false;
            }

            StringBuilder explanation = new();
            explanation.AppendLine(ExtensionResources.FailureThresholdPolicy);
            double failedPercentage = Math.Round(retryFailedTestsPipeServer.FailedTestResults / (double)retryFailedTestsPipeServer.TotalTestRan * 100, 2);
            explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxPercentage, maxPercentage, failedPercentage, retryFailedTestsPipeServer.FailedTestResults, retryFailedTestsPipeServer.TotalTestRan));
            await outputDevice.DisplayAsync(producer, new ErrorMessageOutputDeviceData(explanation.ToString()), cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, out string[]? retryFailedTestsMaxCount))
        {
            int maxCount = int.Parse(retryFailedTestsMaxCount[0], CultureInfo.InvariantCulture);
            int failedTests = retryFailedTestsPipeServer.FailedTests.Count;
            if (failedTests <= maxCount)
            {
                return false;
            }

            StringBuilder explanation = new();
            explanation.AppendLine(ExtensionResources.FailureThresholdPolicy);
            explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxCount, maxCount, failedTests));
            await outputDevice.DisplayAsync(producer, new ErrorMessageOutputDeviceData(explanation.ToString()), cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }
}
