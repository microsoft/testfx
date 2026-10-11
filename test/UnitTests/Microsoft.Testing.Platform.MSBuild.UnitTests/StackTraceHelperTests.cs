// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class StackTraceHelperTests
{
    [TestMethod]
    public void TryFindLocationFromStackFrame_WhenLocationExists_ReturnsLocation()
    {
        const string stackTrace = "   at Program.Main() in /repo/Program.cs:line 42";

        bool foundLocation = StackTraceHelper.TryFindLocationFromStackFrame(stackTrace, out string? file, out int lineNumber, out string? place);

        Assert.IsTrue(foundLocation);
        Assert.AreEqual("/repo/Program.cs", file);
        Assert.AreEqual(42, lineNumber);
        Assert.AreEqual("Program.Main()", place);
    }

    [TestMethod]
    public void TryFindLocationFromStackFrame_WhenLocationIsMissing_ReturnsFalse()
    {
        const string stackTrace = "   at Program.Main()";

        bool foundLocation = StackTraceHelper.TryFindLocationFromStackFrame(stackTrace, out string? file, out int lineNumber, out string? place);

        Assert.IsFalse(foundLocation);
        Assert.IsNull(file);
        Assert.AreEqual(0, lineNumber);
        Assert.IsNull(place);
    }

    [TestMethod]
    public void TryFindLocationFromStackFrame_InitializesRegexWithBoundedTimeout()
    {
        const string stackTrace = "   at Program.Main() in /repo/Program.cs:line 42";
        FieldInfo regexField = typeof(StackTraceHelper).GetField("s_regex", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve StackTraceHelper.s_regex.");
        regexField.SetValue(null, null);

        Assert.IsTrue(StackTraceHelper.TryFindLocationFromStackFrame(stackTrace, out _, out _, out _));

        var regex = (Regex?)regexField.GetValue(null);

        Assert.IsNotNull(regex);
        Assert.AreEqual(StackTraceRegexHelper.MatchTimeout, regex.MatchTimeout);
        Assert.AreNotEqual(Regex.InfiniteMatchTimeout, regex.MatchTimeout);
        Assert.IsTrue(regex.Options.HasFlag(RegexOptions.Compiled));
        Assert.AreEqual(
            @"^   at (?<code>.+) in (?<file>.+):line (?<line>\d+)$",
            regex.ToString());
    }

    [TestMethod]
    public void CreateFrameRegexPattern_WithOptionalLocation_MatchesBothFrameShapes()
    {
        string pattern = StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true);
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);

        Assert.AreEqual(
            @"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$",
            pattern);

        Match withLocation = regex.Match("   at Program.Main() in /repo/Program.cs:line 42");
        Assert.IsTrue(withLocation.Success);
        Assert.AreEqual("Program.Main()", withLocation.Groups["code"].Value);
        Assert.AreEqual("/repo/Program.cs", withLocation.Groups["file"].Value);
        Assert.AreEqual("42", withLocation.Groups["line"].Value);

        Match withoutLocation = regex.Match("   at Program.Main()");
        Assert.IsTrue(withoutLocation.Success);
        Assert.AreEqual("Program.Main()", withoutLocation.Groups["code1"].Value);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_WithRequiredLocation_ProducesExactPattern()
        => Assert.AreEqual(
            @"^   at (?<code>.+) in (?<file>.+):line (?<line>\d+)$",
            StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: false));
}
