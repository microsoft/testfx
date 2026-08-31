// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class ProgressReportingConfiguration
{
#pragma warning disable SA1310 // Field names should not contain underscore
    internal const string MTP_PROGRESS_SILENCE_SECONDS = nameof(MTP_PROGRESS_SILENCE_SECONDS);
    internal const string MTP_PROGRESS_SLOW_TEST_SECONDS = nameof(MTP_PROGRESS_SLOW_TEST_SECONDS);
#pragma warning restore SA1310 // Field names should not contain underscore

    internal static TimeSpan GetThreshold(IEnvironment environment, string variableName, int defaultSeconds)
    {
        string? raw = environment.GetEnvironmentVariable(variableName);
        return !RoslynString.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds >= 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(defaultSeconds);
    }
}
