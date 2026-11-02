// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class StackTraceRegexHelperTests
{
    [TestMethod]
    [DataRow("Namespace.Worker<T>.RunAsync(System.Collections.Generic.List<T> values)", @"C:\work dir\project:core\Worker.cs", 42)]
    [DataRow("Namespace.Worker+<RunAsync>d__12.MoveNext()", "/home/user/source dir/project:core/Worker.cs", 731)]
    public void CreateFrameRegexPattern_FrameWithLocation_CapturesCodeFileAndLine(string code, string file, int line)
    {
        Regex regex = CreateRegex(matchFramesWithoutLocation: true);
        string expectedLine = line.ToString(CultureInfo.InvariantCulture);
        string frame = CreateLocalizedLocationFrame(code, file, expectedLine);

        Match match = regex.Match(frame);

        Assert.IsTrue(match.Success);
        Assert.AreEqual(code, match.Groups["code"].Value);
        Assert.AreEqual(file, match.Groups["file"].Value);
        Assert.AreEqual(expectedLine, match.Groups["line"].Value);
        Assert.IsFalse(match.Groups["code1"].Success);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_FrameWithoutLocation_CapturesCodeOnlyWhenAllowed()
    {
        const string code = "Namespace.Worker+<RunAsync>d__12.MoveNext()";
        Regex locationOnlyRegex = CreateRegex(matchFramesWithoutLocation: false);
        string frame = GetLocalizedAtPrefix() + code;

        Match permissiveMatch = CreateRegex(matchFramesWithoutLocation: true).Match(frame);
        Match locationOnlyMatch = locationOnlyRegex.Match(frame);

        Assert.IsTrue(permissiveMatch.Success);
        Assert.AreEqual(code, permissiveMatch.Groups["code1"].Value);
        Assert.IsFalse(permissiveMatch.Groups["code"].Success);
        Assert.IsFalse(permissiveMatch.Groups["file"].Success);
        Assert.IsFalse(permissiveMatch.Groups["line"].Success);
        Assert.IsFalse(locationOnlyMatch.Success);
    }

    [TestMethod]
    public void CreateFrameRegexPattern_TextThatIsNotExactlyAFrame_DoesNotMatch()
    {
        Regex regex = CreateRegex(matchFramesWithoutLocation: false);
        string localizedFrame = CreateLocalizedLocationFrame("Namespace.Type.Method()", "/src/File.cs", "7");
        Match localizedMatch = regex.Match(localizedFrame);
        Assert.IsTrue(localizedMatch.Success);

        string nonNumericLine = ReplaceCapture(localizedFrame, localizedMatch.Groups["line"], "seven");
        string[] invalidFrames =
        [
            localizedFrame.Substring(1),
            " " + localizedFrame,
            "prefix" + localizedFrame,
            nonNumericLine,
            localizedFrame + " trailing text",
        ];

        foreach (string invalidFrame in invalidFrames)
        {
            Assert.IsFalse(regex.IsMatch(invalidFrame), $"Unexpected match: {invalidFrame}");
        }
    }

    [TestMethod]
    public void GetFrameRegex_MatchesFrameFormatForCurrentTarget()
    {
#if NET7_0_OR_GREATER
        const string expectedCode = "Namespace.Type.Method()";
        const string frame = $"   at {expectedCode}";
#else
        string frame = CaptureRuntimeStackFrame();
        string expectedCode = nameof(CaptureRuntimeStackFrame);
#endif

        Match match = StackTraceHelper.GetFrameRegex().Match(frame);

        Assert.IsTrue(match.Success, $"The runtime frame was not recognized: {frame}");
        Group code = match.Groups["code"].Success ? match.Groups["code"] : match.Groups["code1"];
        Assert.Contains(expectedCode, code.Value);
    }

#if NET7_0_OR_GREATER
    [TestMethod]
    public void GetFrameRegex_FrameWithLocation_CapturesCodeFileAndLine()
    {
        const string frame = "   at Namespace.Type.Method() in /src/File.cs:line 7";

        Match match = StackTraceHelper.GetFrameRegex().Match(frame);

        Assert.IsTrue(match.Success);
        Assert.AreEqual("Namespace.Type.Method()", match.Groups["code"].Value);
        Assert.AreEqual("/src/File.cs", match.Groups["file"].Value);
        Assert.AreEqual("7", match.Groups["line"].Value);
        Assert.IsFalse(match.Groups["code1"].Success);
    }
#endif

    [TestMethod]
    public void GetFrameRegex_HasExplicitCapturesAndNoTimeout()
    {
        Regex regex = StackTraceHelper.GetFrameRegex();

        Assert.AreEqual(RegexOptions.ExplicitCapture, regex.Options & RegexOptions.ExplicitCapture);
        Assert.AreEqual(Regex.InfiniteMatchTimeout, regex.MatchTimeout);

#if !NET7_0_OR_GREATER
        Assert.AreEqual(RegexOptions.Compiled, regex.Options & RegexOptions.Compiled);
        Assert.AreSame(regex, StackTraceHelper.GetFrameRegex());
#endif
    }

    private static Regex CreateRegex(bool matchFramesWithoutLocation)
        => new(
            StackTraceRegexHelper.CreateFrameRegexPattern(matchFramesWithoutLocation),
            RegexOptions.ExplicitCapture);

    private static string CreateLocalizedLocationFrame(string code, string file, string line)
        => GetLocalizedAtPrefix()
            + code
            + " "
            + string.Format(CultureInfo.InvariantCulture, GetInFileLineNumberFormat(), file, line);

    private static string GetLocalizedAtPrefix()
    {
        string frame = CaptureRuntimeStackFrame();
        Match match = CreateRegex(matchFramesWithoutLocation: true).Match(frame);

        Assert.IsTrue(match.Success, $"The localized runtime frame was not recognized: {frame}");
        Group code = match.Groups["code"].Success ? match.Groups["code"] : match.Groups["code1"];
        Assert.IsTrue(code.Success);

        return frame.Substring(0, code.Index);
    }

    private static string GetInFileLineNumberFormat()
    {
        const string resourceName = "StackTrace_InFileLineNumber";
#pragma warning disable RS0030 // Do not use banned APIs
        MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod(
            "GetResourceString",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(string)],
            modifiers: null);
#pragma warning restore RS0030 // Do not use banned APIs
        string? format = (string?)getResourceStringMethod?.Invoke(null, [resourceName]);

        return format is null or resourceName ? "in {0}:line {1}" : format;
    }

    private static string ReplaceCapture(string value, Group capture, string replacement)
        => value.Substring(0, capture.Index)
            + replacement
            + value.Substring(capture.Index + capture.Length);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CaptureRuntimeStackFrame()
    {
        try
        {
            throw new InvalidOperationException("Capture a runtime-formatted frame.");
        }
        catch (InvalidOperationException ex)
        {
            return ex.StackTrace!.Split([Environment.NewLine], StringSplitOptions.None)[0];
        }
    }
}
