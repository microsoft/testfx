// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class TimeoutHelper
{
    static TimeoutHelper()
    {
#pragma warning disable RS0030 // Do not use banned APIs
        string? customAntiHangTimeout = Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT);
#pragma warning restore RS0030 // Do not use banned APIs

        // Took from Tpv2 experience, it's the timeout for the testhost.exe start.
        DefaultHangTimeSpanTimeout = TimeSpan.FromSeconds(90);

        if (customAntiHangTimeout is not null)
        {
            if (TimeSpanParser.TryParse(customAntiHangTimeout, out TimeSpan customHangTimeout))
            {
                DefaultHangTimeSpanTimeout = customHangTimeout;
            }
        }

        DefaultHangTimeoutSeconds = (int)DefaultHangTimeSpanTimeout.TotalSeconds;
        DefaultHangTimeoutMilliseconds = (int)DefaultHangTimeSpanTimeout.TotalMilliseconds;
    }

    /// <summary>
    /// Gets defaultAntiHangTimeout* values are used as timeout for every wait operation in the test platform.
    /// Are not intended for any timeout logic, but only to avoid infinite waits in case of test platform hangs.
    /// </summary>
    public static int DefaultHangTimeoutMilliseconds { get; private set; }

    public static int DefaultHangTimeoutSeconds { get; private set; }

    public static TimeSpan DefaultHangTimeSpanTimeout { get; }
}
