// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[Embedded]
internal static class TimeoutHelper
{
    static TimeoutHelper()
    {
#pragma warning disable RS0030 // Do not use banned APIs
        string? customAntiHangTimeout = Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT);
#pragma warning restore RS0030 // Do not use banned APIs

        DefaultHangTimeSpanTimeout =
            TimeSpanParser.TryParse(customAntiHangTimeout, out TimeSpan customHangTimeout)
            ? customHangTimeout
            : TimeSpan.FromMinutes(5);

        DefaultHangTimeoutSeconds = DefaultHangTimeSpanTimeout.TotalSeconds;
    }

    public static double DefaultHangTimeoutSeconds { get; }

    public static TimeSpan DefaultHangTimeSpanTimeout { get; }
}
