// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class HumanReadableDurationFormatterTests
{
    [TestMethod]
    public void Render_WhenDurationIsNull_ReturnsEmptyString()
        => Assert.AreEqual(string.Empty, HumanReadableDurationFormatter.Render(null));

    [TestMethod]
    [DataRow(0, true, "(0s)")]
    [DataRow(5, true, "(5s)")]
    [DataRow(65, true, "(1m 05s)")]
    [DataRow(3599, true, "(59m 59s)")]
    [DataRow(0, false, "0s")]
    [DataRow(5, false, "5s")]
    [DataRow(59, false, "59s")]
    [DataRow(65, false, "1m 05s")]
    [DataRow(3599, false, "59m 59s")]
    public void Render_WhenSubHourDurationDoesNotShowMilliseconds_FormatsDuration(int totalSeconds, bool wrapInParentheses, string expected)
        => Assert.AreEqual(expected, HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(totalSeconds), wrapInParentheses));

    [TestMethod]
    public void Render_WhenSubHourDurationShowsMilliseconds_FormatsDurationWithMilliseconds()
        => Assert.AreEqual("(1m 05s 123ms)", HumanReadableDurationFormatter.Render(TimeSpan.FromMilliseconds(65_123), showMilliseconds: true));

    [TestMethod]
    public void Render_WhenSubHourDurationIsNegative_PreservesExistingFormatting()
        => Assert.AreEqual("(0s)", HumanReadableDurationFormatter.Render(TimeSpan.FromMinutes(-1)));

    [TestMethod]
    public void Render_WhenDurationHasHours_FormatsDurationWithHours()
        => Assert.AreEqual("(1h 02m 03s)", HumanReadableDurationFormatter.Render(new TimeSpan(0, 1, 2, 3)));
}
