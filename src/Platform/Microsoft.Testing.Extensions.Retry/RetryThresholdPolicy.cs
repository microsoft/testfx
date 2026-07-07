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
        double? maxFailedTests = null;
        double? maxPercentage = null;
        double? maxCount = null;
        if (commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, out string[]? retryFailedTestsMaxPercentage))
        {
            maxPercentage = double.Parse(retryFailedTestsMaxPercentage[0], CultureInfo.InvariantCulture);
            maxFailedTests = maxPercentage / 100 * retryFailedTestsPipeServer.TotalTestRan;
        }

        if (commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, out string[]? retryFailedTestsMaxCount))
        {
            maxCount = double.Parse(retryFailedTestsMaxCount[0], CultureInfo.InvariantCulture);
            maxFailedTests = maxCount.Value;
        }

        // If threshold policy is not enabled, or the failed set is within the threshold, keep retrying.
        if (maxFailedTests is null || (retryFailedTestsPipeServer.FailedUID?.Count ?? 0) <= maxFailedTests)
        {
            return false;
        }

        StringBuilder explanation = new();
        explanation.AppendLine(ExtensionResources.FailureThresholdPolicy);
        if (maxPercentage is not null)
        {
            double failedPercentage = Math.Round(retryFailedTestsPipeServer.FailedUID!.Count / (double)retryFailedTestsPipeServer.TotalTestRan * 100, 2);
            explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxPercentage, maxPercentage, failedPercentage, retryFailedTestsPipeServer.FailedUID.Count, retryFailedTestsPipeServer.TotalTestRan));
        }

        if (maxCount is not null)
        {
            explanation.AppendLine(string.Format(CultureInfo.InvariantCulture, ExtensionResources.FailureThresholdPolicyMaxCount, maxCount, retryFailedTestsPipeServer.FailedUID!.Count));
        }

        await outputDevice.DisplayAsync(producer, new ErrorMessageOutputDeviceData(explanation.ToString()), cancellationToken).ConfigureAwait(false);
        return true;
    }
}
