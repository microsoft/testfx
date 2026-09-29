// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class PackagedAppTestHostLauncher
{
    // Selects the controller connect-back values, dotnet-test execution identity, TRX/HangDump
    // endpoints, and Retry attempt/run correlation metadata that an AUMID-activated host would not
    // otherwise inherit. Unrelated environment values remain excluded because they can contain user
    // data or secrets.
    internal static IEnumerable<KeyValuePair<string, string?>> GetConnectBackEnvironment(TestHostLaunchContext context)
        => GetConnectBackEnvironment(context, retryArtifactManifestPath: null);

    internal static IEnumerable<KeyValuePair<string, string?>> GetConnectBackEnvironment(
        TestHostLaunchContext context,
        string? retryArtifactManifestPath)
    {
        bool isRetryChild = PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(context.Arguments) is null
            && PackagedAppConnectBackHandshake.TryGetHandshakeId(context.Arguments) is not null;
        bool skipExtensionMarkerIncluded = false;
        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            if (environmentVariable.Key.StartsWith(ConnectBackEnvironmentVariablePrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, DotnetTestExecutionIdEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, CtrfReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, HtmlReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, JUnitReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, RetryAttemptEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, RetryRecoveredArtifactManifestEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, LogicalRunIdEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, AppModelControllerExtensionsEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, TrxTestRunIdEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, TrxPipeEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, HangDumpPipeEnvironmentVariableName, StringComparison.OrdinalIgnoreCase))
            {
                skipExtensionMarkerIncluded |= string.Equals(
                    environmentVariable.Key,
                    TestHostControllerSkipExtensionEnvironmentVariableName,
                    StringComparison.OrdinalIgnoreCase);
                yield return string.Equals(
                    environmentVariable.Key,
                    RetryRecoveredArtifactManifestEnvironmentVariableName,
                    StringComparison.OrdinalIgnoreCase)
                    && retryArtifactManifestPath is not null
                        ? new KeyValuePair<string, string?>(environmentVariable.Key, retryArtifactManifestPath)
                        : environmentVariable;
            }
        }

        if (isRetryChild && !skipExtensionMarkerIncluded)
        {
            yield return new(
                TestHostControllerSkipExtensionEnvironmentVariableName,
                "1");
        }
    }
}
