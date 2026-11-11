// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestAssertExtensionsTests : IDisposable
{
    private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

    // The exact diagnostic expectations use English framework resources within this test's ExecutionContext.
    public TestAssertExtensionsTests() => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    public void FileAssertions_DistinguishFilesDirectoriesAndMissingPaths()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "existing file.txt");
            string missing = Path.Combine(directory, "missing");
            File.WriteAllText(file, "content");

            Assert.FileExists(file);
            Assert.DirectoryDoesNotExist(file);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileDoesNotExist(file));
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryExists(file));

            Assert.DirectoryExists(directory);
            Assert.FileDoesNotExist(directory);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryDoesNotExist(directory));
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(directory));

            Assert.FileDoesNotExist(missing);
            Assert.DirectoryDoesNotExist(missing);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(missing));
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryExists(missing));

            string unavailable = Path.Combine(file, "child");
            Assert.FileDoesNotExist(unavailable);
            Assert.DirectoryDoesNotExist(unavailable);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(unavailable));
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryExists(unavailable));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(null, DisplayName = "Filesystem_Null")]
    [DataRow("", DisplayName = "Filesystem_Empty")]
    [DataRow(" ", DisplayName = "Filesystem_Space")]
    [DataRow("invalid\0path", DisplayName = "Filesystem_Nul")]
    public void FileSystemAssertions_InvalidPaths_PreserveExistsSemantics(string? path)
    {
        Assert.FileDoesNotExist(path);
        Assert.DirectoryDoesNotExist(path);
        Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(path));
        Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryExists(path));
    }

    [TestMethod]
    [DataRow(null, true, DisplayName = "NullOrEmpty_Null")]
    [DataRow("", true, DisplayName = "NullOrEmpty_Empty")]
    [DataRow(" ", false, DisplayName = "NullOrEmpty_Space")]
    [DataRow("\t\r\n", false, DisplayName = "NullOrEmpty_Controls")]
    [DataRow("\u2003", false, DisplayName = "NullOrEmpty_UnicodeWhitespace")]
    [DataRow("\0", false, DisplayName = "NullOrEmpty_Nul")]
    [DataRow("value", false, DisplayName = "NullOrEmpty_Text")]
    public void StringAssertions_NullOrEmpty_UsesBclSemantics(string? value, bool nullOrEmpty)
    {
        if (nullOrEmpty)
        {
            Assert.IsNullOrEmpty(value);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.IsNotNullOrEmpty(value));
        }
        else
        {
            Assert.IsNotNullOrEmpty(value);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.IsNullOrEmpty(value));
        }
    }

    [TestMethod]
    [DataRow(null, false, DisplayName = "NotWhitespace_Null")]
    [DataRow("", false, DisplayName = "NotWhitespace_Empty")]
    [DataRow(" ", false, DisplayName = "NotWhitespace_Space")]
    [DataRow("\t\r\n", false, DisplayName = "NotWhitespace_Controls")]
    [DataRow("\u0085\u00a0\u2003\u2028\u2029", false, DisplayName = "NotWhitespace_UnicodeWhitespace")]
    [DataRow("\0", true, DisplayName = "NotWhitespace_Nul")]
    [DataRow("\u200b", true, DisplayName = "NotWhitespace_ZeroWidthSpace")]
    [DataRow(" value\t", true, DisplayName = "NotWhitespace_Mixed")]
    public void IsNotNullOrWhiteSpace_UsesBclSemantics(string? value, bool notWhitespace)
    {
        if (notWhitespace)
        {
            Assert.IsNotNullOrWhiteSpace(value);
        }
        else
        {
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.IsNotNullOrWhiteSpace(value));
        }
    }

    [TestMethod]
    [DataRow(null, "<null>", DisplayName = "Diagnostics_Null")]
    [DataRow("", "\"\"", DisplayName = "Diagnostics_Empty")]
    [DataRow(" ", "\" \"", DisplayName = "Diagnostics_Space")]
    [DataRow("\t\r\n", "\"\\t\\r\\n\"", DisplayName = "Diagnostics_LineBreaks")]
    [DataRow("\u2003", "\"\\u2003\"", DisplayName = "Diagnostics_UnicodeWhitespace")]
    public void StringAssertions_Failures_DistinguishAndEscapeInputs(string? value, string expected)
    {
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.IsNotNullOrWhiteSpace(value, "custom message"));

        AssertFailureMessage(
            $"""
            Assertion failed. Expected condition to be true.
            Assert.IsNotNullOrWhiteSpace(value) failed. Actual: {expected}. custom message

            actual: false
            """,
            failure);
    }

    [TestMethod]
    public void IsNullOrEmpty_Failure_EscapesQuotesBackslashesControlsAndSurrogates()
    {
        const string Value = "\"\\\0\b\f\u001b\ud800";
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() => Assert.IsNullOrEmpty(Value));

        AssertFailureMessage(
            """
            Assertion failed. Expected condition to be true.
            Assert.IsNullOrEmpty(Value) failed. Actual: "\"\\\u0000\u0008\u000c\u001b\ud800".

            actual: false
            """,
            failure);
    }

    [TestMethod]
    public void FileSystemAssertions_Failures_IncludeExpressionPathAndMessage()
    {
        string? path = null;
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.FileExists(path, message: "keep this message"));
        AssertFailureMessage(
            """
            Assertion failed. Expected condition to be true.
            Assert.FileExists(path) failed. Actual: <null>. keep this message

            actual: false
            """,
            failure);

        const string InvalidPath = "invalid\0path";
        failure = Assert.ThrowsExactly<AssertFailedException>(() => Assert.DirectoryExists(InvalidPath));
        AssertFailureMessage(
            """
            Assertion failed. Expected condition to be true.
            Assert.DirectoryExists(InvalidPath) failed. Actual: "invalid\u0000path".

            actual: false
            """,
            failure);
    }

    [TestMethod]
    [DataRow(false, "", DisplayName = "NegativeDiagnostics_File")]
    [DataRow(true, "", DisplayName = "NegativeDiagnostics_Directory")]
    [DataRow(false, "\u00a0\u2003", DisplayName = "NegativeDiagnostics_File_UnicodeWhitespace")]
    [DataRow(true, "\u00a0\u2003", DisplayName = "NegativeDiagnostics_Directory_UnicodeWhitespace")]
    public void FileSystemAssertions_NegativeFailures_IncludeEvaluatedPath(bool useDirectory, string directorySuffix)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + directorySuffix);
        Directory.CreateDirectory(directory);
        try
        {
            string path = useDirectory ? directory : Path.Combine(directory, "existing file.txt");
            if (!useDirectory)
            {
                File.WriteAllText(path, "content");
            }

            int evaluations = 0;
            AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
            {
                if (useDirectory)
                {
                    Assert.DirectoryDoesNotExist(GetPath(), "custom");
                }
                else
                {
                    Assert.FileDoesNotExist(GetPath(), "custom");
                }
            });

            Assert.AreEqual(1, evaluations);
            string assertion = useDirectory ? "DirectoryDoesNotExist" : "FileDoesNotExist";
            string escapedPath = string.Concat(path.Select(character => character switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\r' => @"\r",
                '\n' => @"\n",
                '\t' => @"\t",
                _ when char.IsControl(character) || (char.IsWhiteSpace(character) && character != ' ') || char.IsSurrogate(character)
                    => @"\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            }));
            AssertFailureMessage(
                $"""
                Assertion failed. Expected condition to be true.
                Assert.{assertion}(GetPath()) failed. Actual: "{escapedPath}". custom

                actual: false
                """,
                failure);

            string GetPath()
            {
                evaluations++;
                return path;
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(null, "<null>", DisplayName = "NonEmptyDiagnostics_Null")]
    [DataRow("", "\"\"", DisplayName = "NonEmptyDiagnostics_Empty")]
    public void IsNotNullOrEmpty_Failure_PreservesValueAndMessage(string? value, string expected)
    {
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.IsNotNullOrEmpty(value, message: "custom"));

        AssertFailureMessage(
            $"""
            Assertion failed. Expected condition to be true.
            Assert.IsNotNullOrEmpty(value) failed. Actual: {expected}. custom

            actual: false
            """,
            failure);
    }

    [TestMethod]
    public void Assertions_EvaluateArgumentsOnceInOrder()
    {
        List<string> evaluations = [];
        Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(GetPath(), GetMessage()));
        Assert.AreSequenceEqual(["path", "message"], evaluations);

        string? GetPath()
        {
            evaluations.Add("path");
            return null;
        }

        string GetMessage()
        {
            evaluations.Add("message");
            return "custom";
        }
    }

    [TestMethod]
    public void StringAssertions_NonNullPostconditions_PreserveInput()
    {
        string? nonEmpty = GetValue();
        Assert.IsNotNullOrEmpty(nonEmpty);
        Assert.AreEqual(3, nonEmpty.Length);
        Assert.AreEqual(" a ", nonEmpty);

        string? nonWhitespace = GetValue();
        Assert.IsNotNullOrWhiteSpace(nonWhitespace);
        Assert.AreEqual(3, nonWhitespace.Length);
        Assert.AreEqual(" a ", nonWhitespace);

        static string? GetValue() => " a ";
    }

    [TestMethod]
    public void Assertions_WithinScope_CollectFailuresAndContinue()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "existing.txt");
            File.WriteAllText(file, "content");
            int completed = 0;
            AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
            {
#pragma warning disable MSTESTEXP // Exercise the current framework's experimental assertion scope.
                using (Assert.Scope())
#pragma warning restore MSTESTEXP
                {
                    Assert.FileExists(null);
                    completed++;
                    Assert.FileDoesNotExist(file);
                    completed++;
                    Assert.DirectoryExists(null);
                    completed++;
                    Assert.DirectoryDoesNotExist(directory);
                    completed++;
                    Assert.IsNullOrEmpty("text");
                    completed++;
                    Assert.IsNotNullOrEmpty(null);
                    completed++;
                    Assert.IsNotNullOrWhiteSpace(" ");
                    completed++;
                }
            });

            Assert.AreEqual(7, completed);
            AggregateException failures = Assert.IsInstanceOfType<AggregateException>(failure.InnerException);
            Assert.HasCount(7, failures.InnerExceptions);
            Assert.AreEqual("7 assertion(s) failed within the assert scope.", failure.Message);
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.FileExists(null));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(null, DisplayName = "ContainsEntry_Equal_Null")]
    [DataRow("", DisplayName = "ContainsEntry_Equal_Empty")]
    [DataRow(" ", DisplayName = "ContainsEntry_Equal_Space")]
    [DataRow("\t\r\n", DisplayName = "ContainsEntry_Equal_Controls")]
    [DataRow("value", DisplayName = "ContainsEntry_Equal_Text")]
    public void ContainsEntry_EqualEntries_Succeed(string? value)
    {
        IDictionary<string, string?> dictionary = new Dictionary<string, string?> { ["key"] = value };

        Assert.ContainsEntry("key", value, dictionary);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(42)]
    public void ContainsEntry_ValueTypeEntries_Succeed(int value)
    {
        Dictionary<string, int> dictionary = new() { ["key"] = value };

        Assert.ContainsEntry("key", value, dictionary);
    }

    [TestMethod]
    public void ContainsEntry_UsesDictionaryKeyComparer()
    {
        Dictionary<string, string> dictionary = new(StringComparer.OrdinalIgnoreCase) { ["Stored"] = "value" };

        Assert.ContainsEntry("sToReD", "value", dictionary);
    }

    [TestMethod]
    public void ContainsEntry_Success_UsesValueEqualityAndOneLookupWithoutFormatting()
    {
        object key = new UnformattableValue(1);
        object expected = new UnformattableValue(2);
        object? actual = new UnformattableValue(2);
        Mock<IDictionary<object, object>> dictionary = new(MockBehavior.Strict);
        dictionary.Setup(d => d.TryGetValue(key, out actual)).Returns(true);

        Assert.ContainsEntry(key, expected, dictionary.Object);

        dictionary.Verify(d => d.TryGetValue(key, out actual), Times.Once);
        dictionary.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ContainsEntry_DoesNotUseStructuralValueEquality()
    {
        Dictionary<string, int[]> dictionary = new() { ["key"] = [1, 2] };

        Assert.ThrowsExactly<AssertFailedException>(() => Assert.ContainsEntry("key", new[] { 1, 2 }, dictionary));
    }

    [TestMethod]
    [DataRow(null, "Dictionary is <null>.", DisplayName = "ContainsEntry_Missing_NullDictionary")]
    [DataRow(0, "Dictionary is empty.", DisplayName = "ContainsEntry_Missing_EmptyDictionary")]
    [DataRow(2, "Key was not found (dictionary count: 2).", DisplayName = "ContainsEntry_Missing_PopulatedDictionary")]
    public void ContainsEntry_MissingKey_DistinguishesDictionaryStates(int? count, string reason)
    {
        Dictionary<string, string?>? dictionary = null;
        if (count is { } entryCount)
        {
            dictionary = [];
            for (int i = 0; i < entryCount; i++)
            {
                dictionary.Add($"other-{i}", null);
            }
        }

        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.ContainsEntry("missing", null, dictionary, "custom"));

        AssertFailureMessage(
            $"""
            Assertion failed. Expected condition to be true.
            Assert.ContainsEntry("missing", null, dictionary) failed. Key: "missing". {reason} Expected: <null>. custom

            actual: false
            """,
            failure);
    }

    [TestMethod]
    public void ContainsEntry_MissingKey_DoesNotAcceptDefaultValue()
    {
        Dictionary<string, int> dictionary = [];

        Assert.ThrowsExactly<AssertFailedException>(() => Assert.ContainsEntry("missing", 0, dictionary));
    }

    [TestMethod]
    [DataRow("expected", null, "Key is present, but its value is <null>.", "\"expected\"", "<null>", DisplayName = "ContainsEntry_Mismatch_ActualNull")]
    [DataRow(null, "actual", "Key is present, but a <null> value was expected.", "<null>", "\"actual\"", DisplayName = "ContainsEntry_Mismatch_ExpectedNull")]
    [DataRow(null, "", "Key is present, but a <null> value was expected.", "<null>", "\"\"", DisplayName = "ContainsEntry_Mismatch_NullVersusEmpty")]
    [DataRow("expected", "actual", "Key is present, but its value differs.", "\"expected\"", "\"actual\"", DisplayName = "ContainsEntry_Mismatch_Text")]
    [DataRow("", " ", "Key is present, but its value differs.", "\"\"", "\" \"", DisplayName = "ContainsEntry_Mismatch_EmptyVersusSpace")]
    [DataRow("value", "VALUE", "Key is present, but its value differs.", "\"value\"", "\"VALUE\"", DisplayName = "ContainsEntry_Mismatch_CaseSensitive")]
    [DataRow(42, 42L, "Key is present, but its value differs.", "\"42\" (System.Int32)", "\"42\" (System.Int64)", DisplayName = "ContainsEntry_Mismatch_BoxedNumericTypes")]
    [DataRow("42", 42, "Key is present, but its value differs.", "\"42\"", "\"42\" (System.Int32)", DisplayName = "ContainsEntry_Mismatch_StringVersusNumber")]
    public void ContainsEntry_ValueMismatch_DistinguishesValuesAndTypes(
        object? expectedValue, object? actualValue, string reason, string expected, string actual)
    {
        Dictionary<string, object?> dictionary = new() { ["key"] = actualValue };
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.ContainsEntry("key", expectedValue, dictionary, "custom"));

        AssertFailureMessage(
            $"""
            Assertion failed. Expected condition to be true.
            Assert.ContainsEntry("key", expectedValue, dictionary) failed. Key: "key". {reason} Expected: {expected}. Actual: {actual}. custom

            actual: false
            """,
            failure);
    }

    [TestMethod]
    public void ContainsEntry_Failure_EscapesKeyAndValues()
    {
        const string Key = "key\t\"\\\r\n";
        const string Expected = "\u2003\0";
        Dictionary<string, string> dictionary = new() { [Key] = "\ud800\b\f\u001b" };
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.ContainsEntry(Key, Expected, dictionary));

        AssertFailureMessage(
            """
            Assertion failed. Expected condition to be true.
            Assert.ContainsEntry(Key, Expected, dictionary) failed. Key: "key\t\"\\\r\n". Key is present, but its value differs. Expected: "\u2003\u0000". Actual: "\ud800\u0008\u000c\u001b".

            actual: false
            """,
            failure);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "ContainsEntry_Lookup_Missing")]
    [DataRow(true, DisplayName = "ContainsEntry_Lookup_Mismatch")]
    public void ContainsEntry_Failure_UsesOneLookupWithoutEnumerationOrIndexer(bool found)
    {
        string? actual = found ? "actual" : null;
        Mock<IDictionary<string, string?>> dictionary = new(MockBehavior.Strict);
        dictionary.Setup(d => d.TryGetValue("key", out actual)).Returns(found);
        if (!found)
        {
            dictionary.SetupGet(d => d.Count).Returns(1);
        }

        Assert.ThrowsExactly<AssertFailedException>(() => Assert.ContainsEntry("key", "expected", dictionary.Object));

        dictionary.Verify(d => d.TryGetValue("key", out actual), Times.Once);
        if (!found)
        {
            dictionary.VerifyGet(d => d.Count, Times.Once);
        }

        dictionary.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ContainsEntry_PreservesLookupExceptions()
    {
        InvalidOperationException expected = new("lookup failed");
        Mock<IDictionary<string, string>> dictionary = new(MockBehavior.Strict);
        string? actual = string.Empty;
        dictionary.Setup(d => d.TryGetValue("key", out actual)).Throws(expected);

        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(() => Assert.ContainsEntry("key", "value", dictionary.Object)));
        dictionary.Verify(d => d.TryGetValue("key", out actual), Times.Once);
        dictionary.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void ContainsEntry_PreservesKeyComparerExceptions()
    {
        InvalidOperationException expected = new("comparer failed");
        Mock<IEqualityComparer<string>> comparer = new(MockBehavior.Strict);
        comparer.Setup(c => c.GetHashCode("key")).Returns(1);
        Dictionary<string, string> dictionary = new(comparer.Object) { ["key"] = "value" };
        comparer.Setup(c => c.GetHashCode("key")).Throws(expected);

        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(() => Assert.ContainsEntry("key", "value", dictionary)));
    }

    [TestMethod]
    public void ContainsEntry_Failure_PreservesCallerExpressionsAndEvaluationOrder()
    {
        List<string> evaluations = [];
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(
            () => Assert.ContainsEntry(GetKey(), GetExpected(), GetDictionary(), GetMessage()));

        Assert.AreSequenceEqual(["key", "expected", "dictionary", "message"], evaluations);
        AssertFailureMessage(
            """
            Assertion failed. Expected condition to be true.
            Assert.ContainsEntry(GetKey(), GetExpected(), GetDictionary()) failed. Key: "key". Key is present, but its value differs. Expected: "expected". Actual: "actual". custom

            actual: false
            """,
            failure);

        string GetKey()
        {
            evaluations.Add("key");
            return "key";
        }

        string GetExpected()
        {
            evaluations.Add("expected");
            return "expected";
        }

        Dictionary<string, string> GetDictionary()
        {
            evaluations.Add("dictionary");
            return new() { ["key"] = "actual" };
        }

        string GetMessage()
        {
            evaluations.Add("message");
            return "custom";
        }
    }

    [TestMethod]
    public void ContainsEntry_WithinScope_RecordsOneFailurePerCallAndContinues()
    {
        IDictionary<string, string?>? absent = null;
        Dictionary<string, string?> empty = [];
        Dictionary<string, string?> dictionary = new() { ["null"] = null, ["text"] = "actual" };
        int completed = 0;
        AssertFailedException failure = Assert.ThrowsExactly<AssertFailedException>(() =>
        {
#pragma warning disable MSTESTEXP // Exercise the current framework's experimental assertion scope.
            using (Assert.Scope())
#pragma warning restore MSTESTEXP
            {
                Assert.ContainsEntry("key", "expected", absent);
                completed++;
                Assert.ContainsEntry("key", null, empty);
                completed++;
                Assert.ContainsEntry("missing", null, dictionary);
                completed++;
                Assert.ContainsEntry("null", "expected", dictionary);
                completed++;
                Assert.ContainsEntry("text", null, dictionary);
                completed++;
                Assert.ContainsEntry("text", "expected", dictionary);
                completed++;
                Assert.ContainsEntry("null", null, dictionary);
                completed++;
            }
        });

        Assert.AreEqual(7, completed);
        AggregateException failures = Assert.IsInstanceOfType<AggregateException>(failure.InnerException);
        Assert.HasCount(6, failures.InnerExceptions);
        Assert.AreEqual("6 assertion(s) failed within the assert scope.", failure.Message);
        Assert.ThrowsExactly<AssertFailedException>(() => Assert.ContainsEntry("key", null, empty));
    }

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUICulture;

    private static void AssertFailureMessage(string expected, AssertFailedException failure)
        => Assert.AreEqual(expected.Replace("\r\n", "\n"), failure.Message.Replace("\r\n", "\n"));

    private sealed class UnformattableValue(int value) : IEquatable<UnformattableValue>
    {
        public bool Equals(UnformattableValue? other) => other is not null && value == other.Value;

        public override bool Equals(object? obj) => obj is UnformattableValue other && Equals(other);

        public override int GetHashCode() => value;

        public override string ToString() => throw new InvalidOperationException("Successful assertions must not format keys or values.");

        private int Value => value;
    }
}
