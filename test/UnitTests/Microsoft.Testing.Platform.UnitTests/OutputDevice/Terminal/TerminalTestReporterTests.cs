// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TerminalTestReporterTests : TestBase
{
    public TerminalTestReporterTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void AppendStackFrameFormatsStackTraceLineCorrectly()
    {
        var terminal = new StringBuilderTerminal();
        Exception err;
        try
        {
            throw new Exception();
        }
        catch (Exception ex)
        {
            err = ex;
        }

        string firstStackTraceLine = err.StackTrace!.Replace("\r", string.Empty).Split('\n')[0];
        TerminalTestReporter.AppendStackFrame(terminal, firstStackTraceLine);

#if NETCOREAPP
        Assert.Contains("    at Microsoft.Testing.Platform.UnitTests.TerminalTestReporterTests.AppendStackFrameFormatsStackTraceLineCorrectly() in ", terminal.Output.ToString());
#else
        Assert.Contains("    at Microsoft.Testing.Platform.UnitTests.TerminalTestReporterTests.AppendStackFrameFormatsStackTraceLineCorrectly()", terminal.Output.ToString());
#endif
        // Line number without the respective file
        Assert.That(!terminal.Output.ToString().Contains(" :0"));
    }

    internal class StringBuilderTerminal : ITerminal
    {
        private readonly StringBuilder _stringBuilder;

        public StringBuilderTerminal()
            => _stringBuilder = new();

        public string Output => _stringBuilder.ToString();

        public int Width => throw new NotImplementedException();

        public int Height => throw new NotImplementedException();

        public void Append(char value) => _stringBuilder.Append(value);

        public void Append(string value) => _stringBuilder.Append(value);

        public void AppendLine() => _stringBuilder.AppendLine();

        public void AppendLine(string value) => _stringBuilder.AppendLine(value);

        public void AppendLink(string path, int? lineNumber)
        {
            _stringBuilder.Append(path);
            _stringBuilder.Append(':');
            _stringBuilder.Append(lineNumber);
        }

        public void EraseProgress() => throw new NotImplementedException();

        public void HideCursor() => throw new NotImplementedException();

        public void RenderProgress(TestProgressState?[] progress) => throw new NotImplementedException();

        public void ResetColor()
        {
            // do nothing
        }

        public void SetColor(TerminalColor color)
        {
            // do nothing
        }

        public void ShowCursor() => throw new NotImplementedException();

        public void StartBusyIndicator() => throw new NotImplementedException();

        public void StartUpdate() => throw new NotImplementedException();

        public void StopBusyIndicator() => throw new NotImplementedException();

        public void StopUpdate() => throw new NotImplementedException();
    }
}
