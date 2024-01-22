// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

internal static class LogTestHelpers
{
    public static IEnumerable<LogLevel> GetLogLevels()
        => typeof(LogLevel).GetEnumValues().Cast<LogLevel>();

    public static IEnumerable<(LogLevel DefaultLevel, LogLevel CurrentLevel)> GetLogLevelCombinations()
    {
        List<(LogLevel, LogLevel)> logLevelCombinations = new();
        var logLevels = GetLogLevels().ToArray();

        for (int i = 0; i < logLevels.Length; i++)
        {
            for (int j = 0; i < logLevels.Length; i++)
            {
                logLevelCombinations.Add((logLevels[i], logLevels[j]));
            }
        }

        return logLevelCombinations;
    }

    public static Times GetExpectedLogCallTimes(LogLevel defaultLevel, LogLevel currentLevel)
        => IsLogEnabled(defaultLevel, currentLevel) ? Times.Once() : Times.Never();

    public static bool IsLogEnabled(LogLevel defaultLevel, LogLevel currentLevel)
        => currentLevel >= defaultLevel;
}
