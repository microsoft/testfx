// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    /// <summary>
    /// Reports a live-publishing problem both to the diagnostic log and to the output device, so that
    /// it is visible in CI logs (as an Azure DevOps warning) instead of only in an opt-in log file.
    /// </summary>
    /// <remarks>
    /// Never throws: this is a best-effort diagnostic invoked from error-recovery paths and from
    /// session teardown, where propagating would turn a warning into a failed run.
    /// </remarks>
    private async Task WarnAsync(string message, CancellationToken cancellationToken)
    {
        TryLogWarning(message);
        await DisplayCoreAsync(new WarningMessageOutputDeviceData(message), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports informational live-publishing progress on the output device.
    /// </summary>
    /// <remarks>
    /// Sent as a <see cref="SessionMessageOutputDeviceData"/> rather than plain text because the
    /// <c>dotnet test</c> pipe deliberately discards informational text, and <c>dotnet test</c> is the
    /// usual way this extension runs in a pipeline. Never throws, for the same reason as <see cref="WarnAsync"/>.
    /// </remarks>
    private Task DisplayAsync(string message, CancellationToken cancellationToken)
        => DisplayCoreAsync(new SessionMessageOutputDeviceData(message), cancellationToken);

    private async Task DisplayCoreAsync(IOutputDeviceData data, CancellationToken cancellationToken)
    {
        try
        {
            await _outputDevice.DisplayAsync(this, data, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The message is already in the diagnostic log (for warnings); losing the console copy is
            // preferable to failing the test run from inside a diagnostic helper.
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingWarningDisplayFailed} {ex.Message}");
        }
    }

    /// <summary>
    /// Logs a warning, swallowing any failure from the logging providers.
    /// </summary>
    /// <remarks>
    /// <see cref="Logger"/> invokes each registered provider directly, so a failing provider would
    /// otherwise propagate out of <see cref="WarnAsync"/> — including out of its own recovery path,
    /// where it would replace the exception being handled — and break its never-throws contract.
    /// </remarks>
    private void TryLogWarning(string message)
    {
        try
        {
            _logger.LogWarning(message);
        }
        catch (Exception)
        {
            // There is nowhere left to report this: the diagnostic logger is the fallback sink.
        }
    }
}
