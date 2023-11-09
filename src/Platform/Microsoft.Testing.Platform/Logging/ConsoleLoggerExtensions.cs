// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Logging;

internal static class ConsoleLoggerExtensions
{
    public static void AddConsoleLogger(this TestApplicationBuilder testApplicationBuilder)
    {
        ArgumentGuard.IsNotNull(testApplicationBuilder);

        testApplicationBuilder.Logging.AddProvider(
            (logLevel, serviceProvider) =>
            {
                IConsole console = serviceProvider.GetConsole();
                IClock clock = serviceProvider.GetClock();
                return new ConsoleLoggerProvider(logLevel, console, clock);
            });
    }
}
