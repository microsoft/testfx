// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

internal static class BrowserSdkSelection
{
    public static string ResolveSdkVersion(
        string dotnetRoot,
        string? requestedVersion,
        string? requestedCommit)
    {
        if ((requestedVersion is null) != (requestedCommit is null))
        {
            throw new AssertFailedException(
                "TESTINGPLATFORM_BROWSER_SDK_VERSION and TESTINGPLATFORM_BROWSER_SDK_COMMIT must be provided together.");
        }

        string sdkDirectory = Path.Combine(dotnetRoot, "sdk");
        if (requestedVersion is null)
        {
            string[] sdkVersions = Directory.Exists(sdkDirectory)
                ? Directory.GetDirectories(sdkDirectory)
                : [];
            return sdkVersions.Length == 1
                ? Path.GetFileName(sdkVersions[0])
                : throw new AssertFailedException(
                    $"The SDK-under-test contains {sdkVersions.Length} SDK directories under '{sdkDirectory}'. "
                    + "Set TESTINGPLATFORM_BROWSER_SDK_VERSION and TESTINGPLATFORM_BROWSER_SDK_COMMIT to select the source-matched SDK.");
        }

        if (Path.GetFileName(requestedVersion) != requestedVersion
            || requestedCommit is null
            || !Regex.IsMatch(requestedCommit, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant))
        {
            throw new AssertFailedException(
                "The requested browser SDK version or commit is invalid.");
        }

        string selectedSdk = Path.Combine(sdkDirectory, requestedVersion);
        string toolsetVersion = Path.Combine(selectedSdk, ".toolsetversion");
        if (!Directory.Exists(selectedSdk) || !File.Exists(toolsetVersion))
        {
            throw new AssertFailedException(
                $"The requested browser SDK '{requestedVersion}' or its .toolsetversion file does not exist under '{sdkDirectory}'.");
        }

        string actualCommit = File.ReadLines(toolsetVersion).FirstOrDefault()?.Trim() ?? string.Empty;
        return actualCommit == requestedCommit
            ? requestedVersion
            : throw new AssertFailedException(
                $"The requested browser SDK '{requestedVersion}' was built from '{actualCommit}', not '{requestedCommit}'.");
    }
}
