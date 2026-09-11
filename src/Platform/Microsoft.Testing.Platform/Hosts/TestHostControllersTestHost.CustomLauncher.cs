// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    [UnsupportedOSPlatform("browser")]
    private async Task<IProcess> LaunchUsingCustomLauncherAsync(
        ITestHostLauncher testHostLauncher,
        ProcessStartInfo processStartInfo,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified — populated from a runtime loop.
        Dictionary<string, string?> environmentVariables = new(StringComparer.Ordinal);
#pragma warning restore IDE0028
        foreach (string key in processStartInfo.EnvironmentVariables.Keys)
        {
            environmentVariables[key] = processStartInfo.EnvironmentVariables[key];
        }

        string? workingDirectory = RoslynString.IsNullOrEmpty(processStartInfo.WorkingDirectory) ? null : processStartInfo.WorkingDirectory;
        TestHostLaunchContext context = new(processStartInfo.FileName, arguments, environmentVariables, workingDirectory);

        await _logger.LogDebugAsync(
            $"Delegating test host launch to '{testHostLauncher.DisplayName}' (UID: '{testHostLauncher.Uid}').").ConfigureAwait(false);
        ITestHostHandle handle = await testHostLauncher.LaunchTestHostAsync(context, cancellationToken).ConfigureAwait(false);
        await _logger.LogDebugAsync(
            $"Test host launched by '{testHostLauncher.Uid}'. Identifier: '{handle.Identifier ?? "<none>"}'.").ConfigureAwait(false);
        return new TestHostHandleToProcessAdapter(handle);
    }
}
