// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Text.RegularExpressions;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

using Match = System.Text.RegularExpressions.Match;
using Resolver = ghactions::Microsoft.Testing.Extensions.StackTraceSourceLocationResolver;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class StackTraceResolutionBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "FramePattern_LocationRequired")]
    [DataRow(true, DisplayName = "FramePattern_LocationOptional")]
    public void FramePattern_OptionalLocationChangesAcceptedFrames(bool optional)
    {
        var regex = new Regex(StackTraceRegexHelper.CreateFrameRegexPattern(optional));
        Match location = regex.Match("   at Tests.Example() in /_/src/Example.cs:line 42");

        Assert.IsTrue(location.Success);
        Assert.AreEqual("Tests.Example()", location.Groups["code"].Value);
        Assert.AreEqual("/_/src/Example.cs", location.Groups["file"].Value);
        Assert.AreEqual("42", location.Groups["line"].Value);
        Match locationless = regex.Match("   at Tests.Example()");
        Assert.AreEqual(optional, locationless.Success);
        if (optional)
        {
            Assert.AreEqual("Tests.Example()", locationless.Groups["code1"].Value);
        }

        Assert.IsFalse(regex.IsMatch("at Tests.Example() in /_/src/Example.cs:line 42"));
    }

    [TestMethod]
    public void Resolve_RejectsMalformedFramesAndPreservesOverflowSentinel()
    {
        using var fixture = new CiReporterFixture();
        fixture.FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        string stack = "\r\nnot a frame\n   at     in /_/ignored.cs:line 1\n"
            + "   at Tests.NoFile() in    :line 2\n"
            + "   at Tests.Overflow() in /_/src/Real.cs:line 2147483648\n"
            + "   at Tests.Later() in /_/src/Later.cs:line 19";

        (string RelativeNormalizedPath, int LineNumber)? actual = Resolver.TryResolve(stack, fixture.Root, fixture.FileSystem.Object, fixture.Logger.Object, skipAssertionFrames: false);

        Assert.IsNotNull(actual);
        Assert.AreEqual("src/Real.cs", actual.Value.RelativeNormalizedPath);
        Assert.AreEqual(0, actual.Value.LineNumber);
        fixture.FileSystem.Verify(value => value.ExistFile(It.IsAny<string>()), Times.Once);
        Assert.IsNull(Resolver.TryResolve("not a frame", fixture.Root, fixture.FileSystem.Object, fixture.Logger.Object, false));
        Assert.IsNull(Resolver.TryResolve(null, fixture.Root, fixture.FileSystem.Object, fixture.Logger.Object, false));
        Assert.IsNull(Resolver.TryResolve("   at Tests.NoRoot()", null, fixture.FileSystem.Object, fixture.Logger.Object, false));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "WorkspacePath_NullFile")]
    [DataRow(1, DisplayName = "WorkspacePath_BlankFile")]
    [DataRow(2, DisplayName = "WorkspacePath_NullRoot")]
    [DataRow(3, DisplayName = "WorkspacePath_InvalidRoot")]
    [DataRow(4, DisplayName = "WorkspacePath_InvalidRelativeFile")]
    [DataRow(5, DisplayName = "WorkspacePath_InvalidDeterministicFile")]
    [DataRow(6, DisplayName = "WorkspacePath_DeterministicTraversal")]
    [DataRow(7, DisplayName = "WorkspacePath_RelativeTraversal")]
    [DataRow(8, DisplayName = "WorkspacePath_InvalidRootedFile")]
    public void WorkspaceRelative_InvalidPathsNeverProbeFileSystem(int partition)
    {
        using var fixture = new CiReporterFixture();
        string? path = partition switch
        {
            0 => null,
            1 => " \t ",
            4 => "invalid\0file.cs",
            5 => "/_/invalid\0file.cs",
            6 => "/_/../outside.cs",
            7 => Path.Combine("..", "outside.cs"),
            8 => fixture.Root + Path.DirectorySeparatorChar + "invalid\0file.cs",
            _ => "src/Valid.cs",
        };
        string? root = partition switch { 2 => null, 3 => "invalid\0root", _ => fixture.Root };

        Assert.IsNull(Resolver.TryMakeWorkspaceRelative(path, root, fixture.FileSystem.Object));

        fixture.FileSystem.Verify(value => value.ExistFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void Resolve_TraceAndPredicateDescribeSelectedFrame()
    {
        using var fixture = new CiReporterFixture();
        fixture.FileSystem.Setup(value => value.ExistFile(It.IsAny<string>())).Returns(true);
        List<string> visited = [];
        string stack = "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual() in /_/Assert.cs:line 2\n"
            + "   at Generated.Wrapper() in /_/Generated.cs:line 3\r\n"
            + "   at User.Test() in /_/src/User.cs:line 31";

        (string RelativeNormalizedPath, int LineNumber)? actual = Resolver.TryResolve(stack, fixture.Root, fixture.FileSystem.Object, fixture.Logger.Object, true, code =>
        {
            visited.Add(code);
            return code == "Generated.Wrapper()";
        });

        Assert.IsNotNull(actual);
        Assert.AreEqual(("src/User.cs", 31), actual.Value);
        Assert.AreSequenceEqual(["Generated.Wrapper()", "User.Test()"], visited);
        Assert.AreSequenceEqual(
        [
            "Skipping stack frame 'Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual()' while resolving the source location.",
            "Skipping stack frame 'Generated.Wrapper()' while resolving the source location.",
            "Resolved source location 'src/User.cs' (line 31).",
        ], fixture.Logs.Select(entry => entry.Message));
        fixture.FileSystem.Verify(value => value.ExistFile(Path.Combine(fixture.Root, "src", "User.cs")), Times.Once);
    }
}
