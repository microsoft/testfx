// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitXmlWriter(
    IFileSystem fileSystem,
    IEnvironment environment,
    ITestFramework testFramework,
    int exitCode,
    CancellationToken cancellationToken)
{
    public async Task WriteXmlAsync(string tempPath, SuiteSet suites)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = false,
            Async = true,
            CheckCharacters = false,
        };

        // FileMode.Create creates a fresh tmp file; the random temp path avoids sibling-process collisions.
        using IFileStream stream = fileSystem.NewFileStream(tempPath, FileMode.Create);

#if NETCOREAPP
#pragma warning disable CA2007 // ConfigureAwait — extension code runs on its own threadpool task, no UI sync context
        await using var writer = XmlWriter.Create(stream.Stream, settings);
#pragma warning restore CA2007
#else
        using var writer = XmlWriter.Create(stream.Stream, settings);
#endif

        await writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(prefix: null, localName: "testsuites", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", suites.Name);
        WriteAttribute(writer, "tests", suites.TotalTests.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "failures", suites.TotalFailures.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "errors", suites.TotalErrors.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "skipped", suites.TotalSkipped.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "time", FormatSeconds(suites.TotalDuration));
        WriteAttribute(writer, "timestamp", FormatTimestamp(suites.Timestamp));

        int suiteId = 0;
        foreach (Suite suite in suites.Suites)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteSuiteAsync(writer, suite, suiteId++).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    internal static string XmlSafeText(string? value)
    {
        // Sanitizes a string so it can be safely written by XmlWriter. XML 1.0 forbids
        // most control chars, lone surrogates, and 0xFFFE/0xFFFF; CheckCharacters is disabled,
        // so invalid chars become U+FFFD (REPLACEMENT CHARACTER) instead of shifting offsets.
        if (RoslynString.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder? sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                int cp = char.ConvertToUtf32(c, value[i + 1]);
                if (cp is >= 0x10000 and <= 0x10FFFF)
                {
                    sb?.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                sb ??= NewBuilder(value, i);
                sb.Append('\uFFFD');
                i++;
                continue;
            }

            if (IsXmlChar(c))
            {
                sb?.Append(c);
                continue;
            }

            sb ??= NewBuilder(value, i);
            sb.Append('\uFFFD');
        }

        return sb is null ? value : sb.ToString();

        static StringBuilder NewBuilder(string original, int copyUpTo)
        {
            var b = new StringBuilder(original.Length);
            if (copyUpTo > 0)
            {
                b.Append(original, 0, copyUpTo);
            }

            return b;
        }
    }

    private async Task WriteSuiteAsync(XmlWriter writer, Suite suite, int suiteId)
    {
        await writer.WriteStartElementAsync(prefix: null, localName: "testsuite", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", suite.Name);
        WriteAttribute(writer, "tests", suite.Tests.Count.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "failures", suite.Failures.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "errors", suite.Errors.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "skipped", suite.Skipped.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "time", FormatSeconds(suite.TotalDuration));
        WriteAttribute(writer, "timestamp", FormatTimestamp(suite.Timestamp));
        WriteAttribute(writer, "hostname", environment.MachineName);
        WriteAttribute(writer, "id", suiteId.ToString(CultureInfo.InvariantCulture));

        await WriteSuitePropertiesAsync(writer).ConfigureAwait(false);

        foreach (TestCase tc in suite.Tests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteTestCaseAsync(writer, tc).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private async Task WriteSuitePropertiesAsync(XmlWriter writer)
    {
        // Emit framework / process metadata as testsuite-level <properties> without bloating each <testcase>.
        await writer.WriteStartElementAsync(null, "properties", null).ConfigureAwait(false);
        await WritePropertyAsync(writer, "test-framework", testFramework.DisplayName).ConfigureAwait(false);
        if (!RoslynString.IsNullOrEmpty(testFramework.Version))
        {
            await WritePropertyAsync(writer, "test-framework-version", testFramework.Version).ConfigureAwait(false);
        }

        if (!RoslynString.IsNullOrEmpty(testFramework.Uid))
        {
            await WritePropertyAsync(writer, "test-framework-uid", testFramework.Uid).ConfigureAwait(false);
        }

        await WritePropertyAsync(writer, "exit-code", exitCode.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WriteTestCaseAsync(XmlWriter writer, TestCase tc)
    {
        await writer.WriteStartElementAsync(prefix: null, localName: "testcase", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", tc.Name);
        WriteAttribute(writer, "classname", tc.ClassName);
        WriteAttribute(writer, "time", FormatSeconds(tc.Result.Duration));

        // JUnit normative child order: <properties> first, then <skipped>/<error>/
        // <failure>, then <system-out>/<system-err>. Surefire/Jenkins parsers depend
        // on this order; do not reorder.
        await WritePropertiesAsync(writer, tc).ConfigureAwait(false);

        switch (tc.Result.Outcome)
        {
            case "skipped":
                await writer.WriteStartElementAsync(null, "skipped", null).ConfigureAwait(false);
                if (!RoslynString.IsNullOrEmpty(tc.Result.ErrorMessage))
                {
                    WriteAttribute(writer, "message", tc.Result.ErrorMessage!);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
                break;

            case "failed":
                await WriteFailureOrErrorAsync(writer, "failure", tc.Result).ConfigureAwait(false);
                break;

            case "errored":
            case "timedOut":
            case "cancelled":
                await WriteFailureOrErrorAsync(writer, "error", tc.Result).ConfigureAwait(false);
                break;

            case "passed":
            default:
                break;
        }

        if (!RoslynString.IsNullOrEmpty(tc.Result.StandardOutput))
        {
            await writer.WriteStartElementAsync(null, "system-out", null).ConfigureAwait(false);
            await writer.WriteStringAsync(XmlSafeText(tc.Result.StandardOutput)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        if (!RoslynString.IsNullOrEmpty(tc.Result.StandardError))
        {
            await writer.WriteStartElementAsync(null, "system-err", null).ConfigureAwait(false);
            await writer.WriteStringAsync(XmlSafeText(tc.Result.StandardError)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WriteFailureOrErrorAsync(XmlWriter writer, string elementName, CapturedTestResult result)
    {
        await writer.WriteStartElementAsync(null, elementName, null).ConfigureAwait(false);
        if (!RoslynString.IsNullOrEmpty(result.ErrorMessage))
        {
            WriteAttribute(writer, "message", result.ErrorMessage!);
        }

        if (!RoslynString.IsNullOrEmpty(result.ExceptionType))
        {
            WriteAttribute(writer, "type", result.ExceptionType!);
        }

        if (!RoslynString.IsNullOrEmpty(result.StackTrace))
        {
            await writer.WriteStringAsync(XmlSafeText(result.StackTrace)).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WritePropertiesAsync(XmlWriter writer, TestCase tc)
    {
        bool hasTraits = tc.Result.Traits is { Count: > 0 };
        bool hasTestPath = !RoslynString.IsNullOrEmpty(tc.TestPath);
        bool isDuplicate = tc.DuplicateIndex > 0;

        if (!hasTraits && !hasTestPath && !isDuplicate && RoslynString.IsNullOrEmpty(tc.Result.Uid))
        {
            return;
        }

        await writer.WriteStartElementAsync(null, "properties", null).ConfigureAwait(false);

        if (!RoslynString.IsNullOrEmpty(tc.Result.Uid))
        {
            await WritePropertyAsync(writer, "uid", tc.Result.Uid).ConfigureAwait(false);
        }

        if (hasTestPath)
        {
            await WritePropertyAsync(writer, "testpath", tc.TestPath!).ConfigureAwait(false);
        }

        if (isDuplicate)
        {
            await WritePropertyAsync(writer, "original-name", tc.OriginalName!).ConfigureAwait(false);
            await WritePropertyAsync(writer, "attempt-index", tc.DuplicateIndex.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await WritePropertyAsync(writer, "attempt-of", tc.DuplicateOf.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }

        if (hasTraits)
        {
            foreach (KeyValuePair<string, string> trait in tc.Result.Traits!)
            {
                // Prefix with "trait." so trait keys cannot collide with reserved names like "uid" or "testpath".
                await WritePropertyAsync(writer, $"trait.{trait.Key}", trait.Value).ConfigureAwait(false);
            }
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WritePropertyAsync(XmlWriter writer, string name, string value)
    {
        await writer.WriteStartElementAsync(null, "property", null).ConfigureAwait(false);
        WriteAttribute(writer, "name", name);
        WriteAttribute(writer, "value", value);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static void WriteAttribute(XmlWriter writer, string name, string value)
        => writer.WriteAttributeString(name, XmlSafeText(value));

    private static bool IsXmlChar(char c)
        => c is '\t' or '\n' or '\r' or (>= '\u0020' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD');

    private static string FormatSeconds(TimeSpan duration)
        => duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset timestamp)
        => timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
}
