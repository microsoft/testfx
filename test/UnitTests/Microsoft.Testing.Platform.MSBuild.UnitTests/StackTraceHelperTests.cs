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

        Assert.IsTrue(StackTraceHelper.TryFindLocationFromStackFrame(stackTrace, out _, out _, out _));

        FieldInfo regexField = typeof(StackTraceHelper).GetField("s_regex", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve StackTraceHelper.s_regex.");
        var regex = (Regex?)regexField.GetValue(null);

        Assert.IsNotNull(regex);
        Assert.AreEqual(StackTraceRegexHelper.MatchTimeout, regex.MatchTimeout);
        Assert.AreNotEqual(Regex.InfiniteMatchTimeout, regex.MatchTimeout);
    }
}
