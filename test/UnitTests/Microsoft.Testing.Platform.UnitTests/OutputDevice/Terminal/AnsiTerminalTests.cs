// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class AnsiTerminalTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnsiTerminal_BatchingAndImmediateWritesPreserveEveryControlSequence(bool batching)
    {
        RecordingConsole console = new();
        AnsiTerminal terminal = new(console.Mock.Object);
        console.Mock.SetupGet(c => c.WindowWidth).Returns(83);
        console.Mock.SetupGet(c => c.WindowHeight).Returns(27);
        Assert.AreEqual(83, terminal.Width);
        Assert.AreEqual(27, terminal.Height);
        console.Mock.SetupGet(c => c.IsOutputRedirected).Returns(true);
        Assert.AreEqual(int.MaxValue, terminal.Width);
        Assert.AreEqual(int.MaxValue, terminal.Height);
        if (batching)
        {
            terminal.StartUpdate();
            Assert.AreEqual(
                "Console is already in batching mode.",
                Assert.ThrowsExactly<InvalidOperationException>(terminal.StartUpdate).Message);
        }

        terminal.Append('a');
        terminal.Append("word");
        terminal.AppendLine();
        terminal.AppendLine("next");
        terminal.SetColor(TerminalColor.DarkMagenta);
        terminal.ResetColor();
        terminal.ShowCursor();
        terminal.HideCursor();
        terminal.MoveCursorUp(2);
        terminal.SetCursorHorizontal(7);
        if (batching)
        {
            Assert.AreEqual(string.Empty, console.Text.ToString());
            terminal.StopUpdate();
            console.Mock.Verify(c => c.Write(It.IsAny<StringBuilder>()), Times.Once);
        }

        Assert.AreEqual(
            "aword" + Environment.NewLine + "next" + Environment.NewLine +
            "\u001b[35m\u001b[m\u001b[?25h\u001b[?25l\u001b[2F" + Environment.NewLine + "\u001b[7G",
            console.Text.ToString());
    }

    [TestMethod]
    public void AnsiTerminal_LinksKeepKnownMissingPathsPlainAndUseSafeRelativeTargets()
    {
        RecordingConsole console = new();
        AnsiTerminal terminal = new(console.Mock.Object);
        terminal.AppendLink(null, 17);
        terminal.AppendLink("  ", null);
        Assert.AreEqual(string.Empty, console.Text.ToString());
        terminal.AppendLink("/_/source/Test.cs", 23);
        Assert.AreEqual("/_/source/Test.cs:23", console.Text.ToString());
        console.Text.Clear();
        string relative = Path.Combine("a folder", "results.trx");
        string full = Path.Combine(Directory.GetCurrentDirectory(), relative);
        terminal.AppendLink(full, 17);
        Assert.AreEqual(
            "\u001b[90m\u001b]8;;" + new Uri(full) + "\u001b\\" + relative + ":17\u001b]8;;\u001b\\\u001b[m",
            console.Text.ToString());
        console.Text.Clear();
        string binary = Path.Combine(Directory.GetCurrentDirectory(), "bin", "test.dll");
        terminal.AppendLink(binary, null);
        Assert.AreEqual(
            "\u001b[90m\u001b]8;;" + new Uri(Path.GetDirectoryName(binary)!) + "\u001b\\" + Path.Combine("bin", "test.dll") + "\u001b]8;;\u001b\\\u001b[m",
            console.Text.ToString());
        console.Text.Clear();
        string sibling = Directory.GetCurrentDirectory() + "-neighbor" + Path.DirectorySeparatorChar + "trace.log";
        terminal.AppendLink(sibling, null);
        Assert.AreEqual(
            "\u001b[90m\u001b]8;;" + new Uri(sibling) + "\u001b\\" + sibling + "\u001b]8;;\u001b\\\u001b[m",
            console.Text.ToString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnsiTerminal_BusyIndicatorUsesOnlySupportedOscAndAlwaysRestoresCursor(bool batching)
    {
        RecordingConsole console = new();
        AnsiTerminal terminal = new(console.Mock.Object);
        if (batching)
        {
            terminal.StartUpdate();
        }

        terminal.StartBusyIndicator();
        terminal.StopBusyIndicator();
        if (batching)
        {
            terminal.StopUpdate();
        }

        Assert.AreEqual(
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "\u001b[?25l\u001b[?25h"
                : "\u001b]9;4;3;\u001b\\\u001b[?25l\u001b]9;4;0;\u001b\\\u001b[?25h",
            console.Text.ToString());
    }

    [TestMethod]
    [DataRow((int)TerminalColor.Black, 30)]
    [DataRow((int)TerminalColor.DarkRed, 31)]
    [DataRow((int)TerminalColor.DarkGreen, 32)]
    [DataRow((int)TerminalColor.DarkYellow, 33)]
    [DataRow((int)TerminalColor.DarkBlue, 34)]
    [DataRow((int)TerminalColor.DarkMagenta, 35)]
    [DataRow((int)TerminalColor.DarkCyan, 36)]
    [DataRow((int)TerminalColor.Gray, 37)]
    [DataRow((int)TerminalColor.Default, 39)]
    [DataRow((int)TerminalColor.DarkGray, 90)]
    [DataRow((int)TerminalColor.Red, 91)]
    [DataRow((int)TerminalColor.Green, 92)]
    [DataRow((int)TerminalColor.Yellow, 93)]
    [DataRow((int)TerminalColor.Blue, 94)]
    [DataRow((int)TerminalColor.Magenta, 95)]
    [DataRow((int)TerminalColor.Cyan, 96)]
    [DataRow((int)TerminalColor.White, 97)]
    [DataRow(999, 999)]
    public void AnsiCodes_EveryForegroundColorUsesExactSequence(int color, int code)
    {
        Assert.AreEqual(code, color);
        Assert.AreEqual("\u001b[" + code.ToString(CultureInfo.InvariantCulture) + "m", AnsiCodes.GetSetColorEscapeCode((TerminalColor)color));
    }

    [TestMethod]
    public void AnsiCodes_TextBoundariesBoldAndCursorIntegersAreExact()
    {
        foreach (string? input in new[] { null, string.Empty, "  " })
        {
            Assert.AreEqual(input ?? string.Empty, AnsiCodes.Colorize(input, TerminalColor.Red));
            Assert.AreEqual(input ?? string.Empty, AnsiCodes.MakeBold(input));
        }

        Assert.AreEqual("\u001b[91mpayload\u001b[m", AnsiCodes.Colorize("payload", TerminalColor.Red));
        Assert.AreEqual("\u001b[1mpayload\u001b[m", AnsiCodes.MakeBold("payload"));
        Assert.AreEqual("\u001b[17D", AnsiCodes.MoveCursorBackward(17));
        Assert.AreEqual("\u001b[0D", AnsiCodes.MoveCursorBackward(0));
        Assert.AreEqual("\u001b[23G", AnsiCodes.SetCursorHorizontal(23));
    }

    [TestMethod]
    [DataRow(null, null, "")]
    [DataRow("net8.0", null, " (net8.0)")]
    [DataRow(null, "x64", " (x64)")]
    [DataRow("net8.0", "x64", " (net8.0|x64)")]
    public void AnsiProgress_DiscoveryUsesDiscoveredCountAndIndependentRuntimeComponents(string? framework, string? architecture, string identity)
    {
        RecordingConsole console = new();
        AnsiTerminal terminal = new(console.Mock.Object);
        AnsiTerminalTestProgressFrame frame = new(80, 24);
        AnsiTerminalTestProgressFrame.RenderedProgressItem rendered = new();
        TestProgressState state = new(1, "discovery.dll", framework, architecture, new ZeroStopwatch(), isDiscovery: true) { DiscoveredTests = 7 };

        frame.AppendTestWorkerProgress(state, rendered, terminal);

        Assert.AreEqual("[\u001b[35m+7\u001b[m] discovery.dll" + identity + "\u001b[76G(0s)", console.Text.ToString());
        Assert.AreEqual(4, rendered.RenderedDurationLength);
    }

    private sealed class RecordingConsole
    {
        public RecordingConsole()
        {
            Mock.Setup(c => c.Write(It.IsAny<string>())).Callback<string>(value => Text.Append(value));
            Mock.Setup(c => c.Write(It.IsAny<char>())).Callback<char>(value => Text.Append(value));
            Mock.Setup(c => c.Write(It.IsAny<StringBuilder>())).Callback<StringBuilder>(value => Text.Append(value));
            Mock.Setup(c => c.WriteLine()).Callback(() => Text.AppendLine());
            Mock.Setup(c => c.WriteLine(It.IsAny<string>())).Callback<string>(value => Text.AppendLine(value));
        }

        public Mock<IConsole> Mock { get; } = new();

        public StringBuilder Text { get; } = new();
    }

    private sealed class ZeroStopwatch : IStopwatch
    {
        public TimeSpan Elapsed => TimeSpan.Zero;

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
