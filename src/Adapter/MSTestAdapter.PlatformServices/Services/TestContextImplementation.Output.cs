// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Diagnostic message and output stream management of <see cref="TestContextImplementation"/>.
/// </summary>
internal sealed partial class TestContextImplementation
{
    private SynchronizedStringBuilder? _stdOutStringBuilder;
    private SynchronizedStringBuilder? _stdErrStringBuilder;
    private SynchronizedStringBuilder? _traceStringBuilder;
    private SynchronizedStringBuilder? _testContextMessageStringBuilder;

    internal SynchronizedStringBuilder StandardOutputBuilder
        => GetOrCreate(ref _stdOutStringBuilder);

    internal SynchronizedStringBuilder StandardErrorBuilder
        => GetOrCreate(ref _stdErrStringBuilder);

    internal SynchronizedStringBuilder TraceBuilder
        => GetOrCreate(ref _traceStringBuilder);

    private SynchronizedStringBuilder TestContextMessageBuilder
        => GetOrCreate(ref _testContextMessageStringBuilder);

    private static SynchronizedStringBuilder GetOrCreate(ref SynchronizedStringBuilder? builder)
        => LazyInitializer.EnsureInitialized(ref builder, static () => new())!;

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void Write(string? message)
    {
        string? msg = message?.Replace("\0", "\\0");
        TestContextMessageBuilder.Append(msg);
        WriteLive(msg, appendLine: false);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void Write(string format, params object?[] args)
    {
        string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
        TestContextMessageBuilder.Append(message);
        WriteLive(message, appendLine: false);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="message">The formatted string that contains the trace message.</param>
    public override void WriteLine(string? message)
    {
        string? msg = message?.Replace("\0", "\\0");
        TestContextMessageBuilder.AppendLine(msg);
        WriteLive(msg, appendLine: true);
    }

    /// <summary>
    /// When overridden in a derived class, used to write trace messages while the
    ///     test is running.
    /// </summary>
    /// <param name="format">The string that contains the trace message.</param>
    /// <param name="args">Arguments to add to the trace message.</param>
    public override void WriteLine(string format, params object?[] args)
    {
        string message = string.Format(CultureInfo.CurrentCulture, format.Replace("\0", "\\0"), args);
        TestContextMessageBuilder.AppendLine(message);
        WriteLive(message, appendLine: true);
    }

    /// <summary>
    /// Gets messages from the testContext writeLines.
    /// </summary>
    /// <returns>The test context messages added so far.</returns>
    public string? GetDiagnosticMessages()
        => _testContextMessageStringBuilder?.ToString();

    /// <summary>
    /// Clears the previous testContext writeline messages.
    /// </summary>
    public void ClearDiagnosticMessages()
        => _testContextMessageStringBuilder?.Clear();

    /// <inheritdoc/>
    public void SetDisplayName(string? displayName)
        => TestDisplayName = displayName;

    /// <inheritdoc/>
    public override void DisplayMessage(MessageLevel messageLevel, string message)
        => _messageLogger?.SendMessage(messageLevel, message);

    internal string? GetAndClearOutput()
        => _stdOutStringBuilder?.GetAndClear();

    internal string? GetAndClearError()
        => _stdErrStringBuilder?.GetAndClear();

    internal string? GetAndClearTrace()
        => _traceStringBuilder?.GetAndClear();
}
