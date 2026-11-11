// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestAssertExtensionsTests
{
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
    [DataRow(false, DisplayName = "NegativeDiagnostics_File")]
    [DataRow(true, DisplayName = "NegativeDiagnostics_Directory")]
    public void FileSystemAssertions_NegativeFailures_IncludeEvaluatedPath(bool useDirectory)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
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
            string escapedPath = path.Replace("\\", "\\\\");
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

    private static void AssertFailureMessage(string expected, AssertFailedException failure)
        => Assert.AreEqual(expected.Replace("\r\n", "\n"), failure.Message.Replace("\r\n", "\n"));
}
