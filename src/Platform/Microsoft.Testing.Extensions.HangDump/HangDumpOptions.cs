// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal static class HangDumpOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    public static bool IsEnabled(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName);

    public static TimeSpan GetTimeout(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : DefaultTimeout;

    /// <summary>
    /// The test host coalesces activity and sends it to the controller at most once per interval instead of once per
    /// test result, so fast tests do not pay a cross-process round trip each.
    /// </summary>
    public static TimeSpan GetActivitySignalInterval(TimeSpan hangTimeout)
    {
        var maxInterval = TimeSpan.FromSeconds(1);
        var minInterval = TimeSpan.FromMilliseconds(10);
        var interval = TimeSpan.FromTicks(hangTimeout.Ticks / 10);
        return interval > maxInterval ? maxInterval : interval < minInterval ? minInterval : interval;
    }

    /// <summary>
    /// Gets how long the controller waits after the last delivered activity signal before taking a hang dump.
    /// Activity that happens right after a delivery can wait up to one signal interval before it is delivered, so
    /// the controller adds that interval to the timeout. A dump then never fires before the configured timeout has
    /// elapsed since the last activity; it can fire at most two intervals later.
    /// </summary>
    public static TimeSpan GetInactivityTimerDueTime(TimeSpan hangTimeout)
        => hangTimeout + GetActivitySignalInterval(hangTimeout);
}
