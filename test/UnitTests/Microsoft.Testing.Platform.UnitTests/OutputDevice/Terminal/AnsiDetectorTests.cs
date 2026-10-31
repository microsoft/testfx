// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice.Terminal;

[TestClass]
public sealed class AnsiDetectorTests
{
    [TestMethod]
    [DataRow(null, false, DisplayName = "Null terminal")]
    [DataRow("", false, DisplayName = "Empty terminal")]
    [DataRow("dumb", false)]
    [DataRow("xterm", true)]
    [DataRow("xterm-256color", true)]
    [DataRow("rxvt-unicode", true)]
    [DataRow("eterm", true)]
    [DataRow("custom-eterm", true)]
    [DataRow("eterm-color", false)]
    [DataRow("eterm-color-256color", false)]
    [DataRow("custom-eterm-color", true)]
    [DataRow("screen-256color", true)]
    [DataRow("custom-tmux-terminal", true)]
    [DataRow("vt100", true)]
    [DataRow("vt102", true)]
    [DataRow("vt220", true)]
    [DataRow("vt320", true)]
    [DataRow("custom-ansi-terminal", true)]
    [DataRow("scoansi", true)]
    [DataRow("custom-cygwin-terminal", true)]
    [DataRow("custom-linux-terminal", true)]
    [DataRow("custom-konsole-terminal", true)]
    [DataRow("custom-bvterm-terminal", true)]
    [DataRow("st-256color", true)]
    [DataRow("custom-alacritty-terminal", true)]
    [DataRow("custom-xterm", false)]
    [DataRow("custom-rxvt", false)]
    [DataRow("custom-screen", false)]
    [DataRow("custom-vt100", false)]
    [DataRow("custom-vt102", false)]
    [DataRow("custom-vt220", false)]
    [DataRow("custom-vt320", false)]
    [DataRow("custom-st-256color", false)]
    [DataRow("XTERM", false)]
    [DataRow("custom-ANSI-terminal", false)]
    [DataRow("\0xterm", false, DisplayName = "Nul before an anchored prefix")]
    public void IsAnsiSupported_TerminalFamilyAndPosition_MatchesContract(string? terminal, bool expected)
        => Assert.AreEqual(expected, AnsiDetector.IsAnsiSupported(terminal));
}
