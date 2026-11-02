// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineParseResultTests
{
    [TestMethod]
    public void Empty_HasNoTool_NoOptions_NoErrors()
    {
        CommandLineParseResult empty = CommandLineParseResult.Empty;

        Assert.IsNull(empty.ToolName);
        Assert.IsFalse(empty.HasTool);
        Assert.IsFalse(empty.HasError);
        Assert.IsEmpty(empty.Options);
        Assert.IsEmpty(empty.Errors);
    }

    [TestMethod]
    public void HasTool_TrueWhenToolNameProvided()
    {
        var result = new CommandLineParseResult("mytool", [], []);

        Assert.IsTrue(result.HasTool);
        Assert.AreEqual("mytool", result.ToolName);
    }

    [TestMethod]
    public void HasTool_FalseWhenToolNameIsNull()
    {
        var result = new CommandLineParseResult(null, [], []);

        Assert.IsFalse(result.HasTool);
    }

    [TestMethod]
    public void HasError_TrueWhenErrorsPresent()
    {
        var result = new CommandLineParseResult(null, [], ["some error"]);

        Assert.IsTrue(result.HasError);
    }

    [TestMethod]
    public void HasError_FalseWhenNoErrors()
    {
        var result = new CommandLineParseResult(null, [], []);

        Assert.IsFalse(result.HasError);
    }

    [TestMethod]
    public void IsOptionSet_ReturnsTrueForKnownOption()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("verbose", [])], []);

        Assert.IsTrue(result.IsOptionSet("verbose"));
    }

    [TestMethod]
    public void IsOptionSet_IsCaseInsensitive()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("verbose", [])], []);

        Assert.IsTrue(result.IsOptionSet("VERBOSE"));
        Assert.IsTrue(result.IsOptionSet("Verbose"));
    }

    [TestMethod]
    public void IsOptionSet_StripsLeadingDashes()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("verbose", [])], []);

        Assert.IsTrue(result.IsOptionSet("--verbose"));
        Assert.IsTrue(result.IsOptionSet("-verbose"));
    }

    [TestMethod]
    public void IsOptionSet_ReturnsFalseForUnknownOption()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("verbose", [])], []);

        Assert.IsFalse(result.IsOptionSet("quiet"));
    }

    [TestMethod]
    public void TryGetOptionArgumentList_ReturnsTrueAndArgumentsForKnownOption()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("output", ["file.txt"])], []);

        bool found = result.TryGetOptionArgumentList("output", out string[]? args);

        Assert.IsTrue(found);
        Assert.IsNotNull(args);
        Assert.HasCount(1, args);
        Assert.AreEqual("file.txt", args[0]);
    }

    [TestMethod]
    public void TryGetOptionArgumentList_ReturnsFalseForUnknownOption()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("output", ["file.txt"])], []);

        bool found = result.TryGetOptionArgumentList("missing", out string[]? args);

        Assert.IsFalse(found);
        Assert.IsNull(args);
    }

    [TestMethod]
    public void TryGetOptionArgumentList_IsCaseSensitive()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("output", ["file.txt"])], []);

        bool found = result.TryGetOptionArgumentList("OUTPUT", out string[]? args);

        Assert.IsFalse(found);
        Assert.IsNull(args);
    }

    [TestMethod]
    public void TryGetOptionArgumentList_StripsLeadingDashes()
    {
        var result = new CommandLineParseResult(null, [new CommandLineParseOption("output", ["file.txt"])], []);

        bool found = result.TryGetOptionArgumentList("--output", out string[]? args);

        Assert.IsTrue(found);
        Assert.IsNotNull(args);
        Assert.HasCount(1, args);
        Assert.AreEqual("file.txt", args[0]);
    }

    [TestMethod]
    public void TryGetOptionArgumentList_CombinesArgumentsFromMultipleOccurrences()
    {
        var result = new CommandLineParseResult(
            null,
            [
                new CommandLineParseOption("filter", ["Class1"]),
                new CommandLineParseOption("filter", ["Class2"]),
            ],
            []);

        bool found = result.TryGetOptionArgumentList("filter", out string[]? args);

        Assert.IsTrue(found);
        Assert.IsNotNull(args);
        Assert.HasCount(2, args);
        Assert.Contains("Class1", args);
        Assert.Contains("Class2", args);
    }

    [TestMethod]
    public void Equals_ReturnsTrueForIdenticalResults()
    {
        var a = new CommandLineParseResult("tool", [new CommandLineParseOption("opt", ["val"])], ["err"]);
        var b = new CommandLineParseResult("tool", [new CommandLineParseOption("opt", ["val"])], ["err"]);

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Equals_ReturnsFalseWhenToolNameDiffers()
    {
        var a = new CommandLineParseResult("tool1", [], []);
        var b = new CommandLineParseResult("tool2", [], []);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_ReturnsFalseWhenErrorsDiffer()
    {
        var a = new CommandLineParseResult(null, [], ["error1"]);
        var b = new CommandLineParseResult(null, [], ["error2"]);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_ReturnsFalseWhenOptionsDiffer()
    {
        var a = new CommandLineParseResult(null, [new CommandLineParseOption("opt1", [])], []);
        var b = new CommandLineParseResult(null, [new CommandLineParseOption("opt2", [])], []);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Equals_ReturnsFalseForNull()
    {
        var a = new CommandLineParseResult(null, [], []);

        Assert.IsFalse(a.Equals(null));
    }

    [TestMethod]
    public void Equals_ReturnsTrueForSameReference()
    {
        var a = new CommandLineParseResult("tool", [], []);

        Assert.IsTrue(a.Equals(a));
    }

    [TestMethod]
    public void ToString_FormatsToolNameErrorsAndOptions()
    {
        var result = new CommandLineParseResult("mytool", [new CommandLineParseOption("opt", ["val"])], ["an error"]);

        string text = result.ToString();

        Assert.AreEqual(
            """
            ToolName: mytool
            Errors:
                an error
            Options:
               opt
                    val

            """.Replace("\r\n", "\n"),
            text.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void ToString_EmptyResult_FormatsEmptyToolNameAndNone()
    {
        string text = CommandLineParseResult.Empty.ToString();

        Assert.AreEqual(
            $"""
            ToolName: {string.Empty}
            Errors:
                None
            Options:
                None

            """.Replace("\r\n", "\n"),
            text.Replace("\r\n", "\n"));
    }

    [TestMethod]
    [DataRow("error-count", DisplayName = "Different error count")]
    [DataRow("error-order", DisplayName = "Different error order")]
    [DataRow("option-count", DisplayName = "Different option count")]
    [DataRow("argument-count", DisplayName = "Different argument count")]
    [DataRow("argument-order", DisplayName = "Different argument order")]
    public void Equals_DistinguishesCollectionCardinalityAndOrder(string difference)
    {
        CommandLineParseResult original = new("tool", [new("filter", ["first", "last"])], ["error-one", "error-two"]);
        CommandLineParseResult different = difference switch
        {
            "error-count" => new("tool", [new("filter", ["first", "last"])], ["error-one"]),
            "error-order" => new("tool", [new("filter", ["first", "last"])], ["error-two", "error-one"]),
            "option-count" => new("tool", [new("filter", ["first", "last"]), new("quiet", [])], ["error-one", "error-two"]),
            "argument-count" => new("tool", [new("filter", ["first"])], ["error-one", "error-two"]),
            "argument-order" => new("tool", [new("filter", ["last", "first"])], ["error-one", "error-two"]),
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };

        Assert.IsFalse(original.Equals(different));
        Assert.IsFalse(different.Equals(original));
        Assert.IsFalse(original.Equals((object)different));
        Assert.IsFalse(original.Equals("tool"));
    }

    [TestMethod]
    public void TryGetOptionArgumentList_SeparatesZeroArityAndInterleavedRepeatedOptions()
    {
        CommandLineParseResult parsed = new(
            null,
            [
                new("filter", ["first"]),
                new("quiet", []),
                new("other", ["unrelated"]),
                new("filter", []),
                new("filter", ["last"]),
            ],
            []);

        Assert.IsTrue(parsed.TryGetOptionArgumentList("--filter", out string[]? filters));
        Assert.AreSequenceEqual(new[] { "first", "last" }, filters);
        Assert.IsTrue(parsed.TryGetOptionArgumentList("-quiet", out string[]? quiet));
        Assert.IsNotNull(quiet);
        Assert.IsEmpty(quiet);
        Assert.IsTrue(parsed.IsOptionSet("QUIET"));
    }

    [TestMethod]
    public void ToString_PreservesMultipleErrorOptionAndArgumentOrdering()
    {
        CommandLineParseResult parsed = new(
            "inspect",
            [new("filter", ["first", "last"]), new("quiet", [])],
            ["first error", "last error"]);

        Assert.AreEqual(
            """
            ToolName: inspect
            Errors:
                first error
                last error
            Options:
               filter
                    first
                    last
               quiet

            """.Replace("\r\n", "\n"),
            parsed.ToString().Replace("\r\n", "\n"));
    }
}
