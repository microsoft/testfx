// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.Logging;

internal static class LogLevelMapper
{
    public static MelLogLevel ToMicrosoftExtensions(MtpLogLevel level)
        => level switch
        {
            MtpLogLevel.Trace => MelLogLevel.Trace,
            MtpLogLevel.Debug => MelLogLevel.Debug,
            MtpLogLevel.Information => MelLogLevel.Information,
            MtpLogLevel.Warning => MelLogLevel.Warning,
            MtpLogLevel.Error => MelLogLevel.Error,
            MtpLogLevel.Critical => MelLogLevel.Critical,
            MtpLogLevel.None => MelLogLevel.None,
            _ => MelLogLevel.None,
        };
}
