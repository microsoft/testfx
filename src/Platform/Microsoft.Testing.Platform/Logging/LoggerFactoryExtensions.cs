// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

public static class LoggerFactoryExtensions
{
    public static ILogger<TCategoryName> CreateLogger<TCategoryName>(this ILoggerFactory factory)
    {
        ArgumentGuard.IsNotNull(factory);
        return new Logger<TCategoryName>(factory);
    }
}
