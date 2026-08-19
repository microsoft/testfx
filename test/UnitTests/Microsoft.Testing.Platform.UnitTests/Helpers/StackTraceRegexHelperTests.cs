// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class StackTraceRegexHelperTests
{
    [TestMethod]
    public void CreateFrameRegexPattern_MatchFramesWithoutLocation_MatchesFrameWithFileAndLine()
    {
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true), RegexOptions.ExplicitCapture);

        Match match = regex.Match("   at MyNamespace.MyClass.MyMethod() in /repo/src/MyClass.cs:line 42");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("MyNamespace.MyClass.MyMethod()", match.Groups["code"].Value);
        Assert.AreEqual("/repo/src/MyClass.cs", match.Groups["file"].Value);
        Assert.AreEqual("42", match.Groups["line"].Value);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_MatchFramesWithoutLocation_MatchesFrameWithoutFileOrLine()
    {
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true), RegexOptions.ExplicitCapture);

        Match match = regex.Match("   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)");

        Assert.IsTrue(match.Success);
        Assert.IsFalse(match.Groups["code"].Success);
        Assert.AreEqual("System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)", match.Groups["code1"].Value);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_MatchFramesWithoutLocationFalse_DoesNotMatchFrameWithoutLocation()
    {
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: false), RegexOptions.ExplicitCapture);

        Match match = regex.Match("   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)");

        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_MatchFramesWithoutLocationFalse_MatchesFrameWithFileAndLine()
    {
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: false), RegexOptions.ExplicitCapture);

        Match match = regex.Match("   at MyNamespace.MyClass.MyMethod() in /repo/src/MyClass.cs:line 42");

        Assert.IsTrue(match.Success);
        Assert.AreEqual("MyNamespace.MyClass.MyMethod()", match.Groups["code"].Value);
        Assert.AreEqual("/repo/src/MyClass.cs", match.Groups["file"].Value);
        Assert.AreEqual("42", match.Groups["line"].Value);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_DoesNotMatchLineWithoutLeadingIndentation()
    {
        // The pattern requires exactly 3 leading spaces before "at", mirroring the .NET stack trace format.
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation: true), RegexOptions.ExplicitCapture);

        Match match = regex.Match("at MyNamespace.MyClass.MyMethod()");

        Assert.IsFalse(match.Success);
    }

    [TestMethod]
    public void MatchTimeout_IsOneSecond()
        => Assert.AreEqual(TimeSpan.FromSeconds(1), StackTraceRegexHelper.MatchTimeout);
}
