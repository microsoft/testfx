// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class BoundedUtf8LineReaderTests
{
    // BoundedUtf8LineReader is linked into each report-engine assembly, so resolve the
    // CtrfReport copy through reflection to avoid an ambiguous type reference.
    private static readonly Type ReaderType =
        typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.BoundedUtf8LineReader")
        ?? throw new InvalidOperationException("Could not find type BoundedUtf8LineReader in the Ctrf report engine assembly.");

    private static readonly Type ResultType =
        typeof(CtrfReportEngine).Assembly.GetType("Microsoft.Testing.Extensions.BoundedLineReadResult")
        ?? throw new InvalidOperationException("Could not find type BoundedLineReadResult in the Ctrf report engine assembly.");

    // Constructor signature: BoundedUtf8LineReader(Stream stream, long maxBytes, int maxLineBytes, int maxLineChars).
    private static readonly ConstructorInfo Constructor =
        ReaderType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, [typeof(Stream), typeof(long), typeof(int), typeof(int)], null)
        ?? throw new InvalidOperationException("Could not resolve BoundedUtf8LineReader constructor.");

    private static readonly MethodInfo ReadLineMethod =
        ReaderType.GetMethod("ReadLine", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Could not resolve BoundedUtf8LineReader.ReadLine.");

    private static readonly string LineResultName = Enum.GetName(ResultType, 0)!;
    private static readonly string EndResultName = Enum.GetName(ResultType, 1)!;
    private static readonly string LimitExceededResultName = Enum.GetName(ResultType, 2)!;

    [TestMethod]
    public void ReadLine_EmptyStream_ReturnsEnd()
    {
        object reader = CreateReader(new MemoryStream(), maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(EndResultName, result);
        Assert.IsNull(line);
    }

    [TestMethod]
    public void ReadLine_SingleLineWithoutTrailingNewline_ReturnsLineAtEndOfStream()
    {
        object reader = CreateReaderFromText("hello world", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(LineResultName, result);
        Assert.AreEqual("hello world", line);

        (string secondResult, string? secondLine) = ReadLine(reader);
        Assert.AreEqual(EndResultName, secondResult);
        Assert.IsNull(secondLine);
    }

    [TestMethod]
    public void ReadLine_LineFeedTerminator_SplitsLinesCorrectly()
    {
        object reader = CreateReaderFromText("first\nsecond\n", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string firstResult, string? firstLine) = ReadLine(reader);
        (string secondResult, string? secondLine) = ReadLine(reader);
        (string thirdResult, string? thirdLine) = ReadLine(reader);

        Assert.AreEqual(LineResultName, firstResult);
        Assert.AreEqual("first", firstLine);
        Assert.AreEqual(LineResultName, secondResult);
        Assert.AreEqual("second", secondLine);
        Assert.AreEqual(EndResultName, thirdResult);
        Assert.IsNull(thirdLine);
    }

    [TestMethod]
    public void ReadLine_CarriageReturnLineFeedTerminator_StripsCarriageReturn()
    {
        object reader = CreateReaderFromText("first\r\nsecond\r\n", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string firstResult, string? firstLine) = ReadLine(reader);
        (string secondResult, string? secondLine) = ReadLine(reader);

        Assert.AreEqual(LineResultName, firstResult);
        Assert.AreEqual("first", firstLine);
        Assert.AreEqual(LineResultName, secondResult);
        Assert.AreEqual("second", secondLine);
    }

    [TestMethod]
    public void ReadLine_LoneCarriageReturnNotFollowedByLineFeed_IsKeptAsPartOfLine()
    {
        object reader = CreateReaderFromText("abc\rdef", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(LineResultName, result);
        Assert.AreEqual("abc\rdef", line);
    }

    [TestMethod]
    public void ReadLine_MultiByteUtf8Characters_DecodesCorrectly()
    {
        // "café" (accented 'é' is 2 UTF-8 bytes) followed by a surrogate-pair emoji (4 UTF-8 bytes).
        string expected = "café\U0001F600";
        object reader = CreateReaderFromText(expected + "\n", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 1024);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(LineResultName, result);
        Assert.AreEqual(expected, line);
    }

    [TestMethod]
    public void ReadLine_TotalBytesBudgetExceeded_ReturnsLimitExceeded()
    {
        object reader = CreateReaderFromText("first\nsecond\n", maxBytes: 8, maxLineBytes: 1024, maxLineChars: 1024);

        (string firstResult, string? firstLine) = ReadLine(reader);
        (string secondResult, string? secondLine) = ReadLine(reader);

        Assert.AreEqual(LineResultName, firstResult);
        Assert.AreEqual("first", firstLine);
        Assert.AreEqual(LimitExceededResultName, secondResult);
        Assert.IsNull(secondLine);
    }

    [TestMethod]
    public void ReadLine_PerLineBytesBudgetExceeded_ReturnsLimitExceeded()
    {
        object reader = CreateReaderFromText("abcdefghij\n", maxBytes: 1024, maxLineBytes: 4, maxLineChars: 1024);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(LimitExceededResultName, result);
        Assert.IsNull(line);
    }

    [TestMethod]
    public void ReadLine_DecodedCharBudgetExceeded_ReturnsLimitExceeded()
    {
        object reader = CreateReaderFromText("abcdefghij\n", maxBytes: 1024, maxLineBytes: 1024, maxLineChars: 4);

        (string result, string? line) = ReadLine(reader);

        Assert.AreEqual(LimitExceededResultName, result);
        Assert.IsNull(line);
    }

    [TestMethod]
    public void ReadLine_MultipleLinesAcrossInternalReadBufferBoundary_ReadsAllLinesCorrectly()
    {
        // The internal read buffer is 8192 bytes; build enough lines to cross that boundary
        // more than once, then verify every line round-trips correctly and in order.
        var expectedLines = new List<string>();
        var builder = new StringBuilder();
        for (int i = 0; i < 2000; i++)
        {
            string currentLine = $"line-{i:D4}";
            expectedLines.Add(currentLine);
            builder.Append(currentLine).Append('\n');
        }

        object reader = CreateReaderFromText(builder.ToString(), maxBytes: long.MaxValue, maxLineBytes: 1024, maxLineChars: 1024);

        foreach (string expectedLine in expectedLines)
        {
            (string result, string? line) = ReadLine(reader);
            Assert.AreEqual(LineResultName, result);
            Assert.AreEqual(expectedLine, line);
        }

        (string finalResult, string? finalLine) = ReadLine(reader);
        Assert.AreEqual(EndResultName, finalResult);
        Assert.IsNull(finalLine);
    }

    private static object CreateReaderFromText(string text, long maxBytes, int maxLineBytes, int maxLineChars)
        => CreateReader(new MemoryStream(Encoding.UTF8.GetBytes(text)), maxBytes, maxLineBytes, maxLineChars);

    private static object CreateReader(Stream stream, long maxBytes, int maxLineBytes, int maxLineChars)
        => Constructor.Invoke([stream, maxBytes, maxLineBytes, maxLineChars]);

    private static (string Result, string? Line) ReadLine(object reader)
    {
        object?[] parameters = [null];
        object result = ReadLineMethod.Invoke(reader, parameters)!;
        return (result.ToString()!, (string?)parameters[0]);
    }
}
