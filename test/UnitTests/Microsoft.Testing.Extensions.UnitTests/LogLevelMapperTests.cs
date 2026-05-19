// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Logging;

using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class LogLevelMapperTests
{
    [DataRow(MtpLogLevel.Trace, MelLogLevel.Trace)]
    [DataRow(MtpLogLevel.Debug, MelLogLevel.Debug)]
    [DataRow(MtpLogLevel.Information, MelLogLevel.Information)]
    [DataRow(MtpLogLevel.Warning, MelLogLevel.Warning)]
    [DataRow(MtpLogLevel.Error, MelLogLevel.Error)]
    [DataRow(MtpLogLevel.Critical, MelLogLevel.Critical)]
    [DataRow(MtpLogLevel.None, MelLogLevel.None)]
    [TestMethod]
    public void ToMicrosoftExtensions_MapsEveryDefinedMtpLevelToMatchingMelLevel(MtpLogLevel input, MelLogLevel expected)
        => Assert.AreEqual(expected, LogLevelMapper.ToMicrosoftExtensions(input));

    [TestMethod]
    public void ToMicrosoftExtensions_EveryEnumValueHasAnExplicitMapping()
    {
        // Locks in coverage: if a new MTP LogLevel value is added in the future without updating the mapper,
        // this test catches the silent fallback to None before it ships.
        foreach (MtpLogLevel value in (MtpLogLevel[])Enum.GetValues(typeof(MtpLogLevel)))
        {
            MelLogLevel mapped = LogLevelMapper.ToMicrosoftExtensions(value);
            Assert.AreEqual(
                value.ToString(),
                mapped.ToString(),
                $"LogLevelMapper.ToMicrosoftExtensions(LogLevel.{value}) returned {mapped}; expected a same-named MEL level. Update LogLevelMapper when adding new MTP LogLevel values.");
        }
    }
}
