// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxLongPathBoundaryBehaviorTests
{
#if NETCOREAPP
    [TestMethod]
    [DataRow("relative")]
    [DataRow("long")]
    [DataRow("unc")]
    [DataRow("extended")]
    public void CoreRuntime_DoesNotResolveOrRewritePaths(string shape)
    {
        string path = shape switch
        {
            "relative" => Path.Combine("subdir", "report.trx"),
            "long" => Path.Combine("subdir", new string('x', 300), "trace.log"),
            "unc" => @"\\server\share\report.trx",
            _ => @"\\?\C:\reports\report.trx",
        };

        string actual = TrxLongPathHelper.GetPathForFileSystemAccess(
            path, _ => throw new InvalidOperationException("The modern runtime must receive the original path directly."));

        Assert.AreEqual(path, actual);
    }
#else
    [TestMethod]
    [DataRow(false, DisplayName = "TrxLongPath_ExistingExtendedPrefix")]
    [DataRow(true, DisplayName = "TrxLongPath_ExistingDevicePrefix")]
    public void Framework_AlreadyExtendedPathsBypassResolution(bool device)
    {
        string path = device ? @"\\.\pipe\not-a-real-connection" : @"\\?\C:\reports\" + new string('x', 300);

        string actual = TrxLongPathHelper.GetPathForFileSystemAccess(
            path, _ => throw new InvalidOperationException("Already extended paths must not be resolved twice."));

        Assert.AreEqual(path, actual);
    }

    [TestMethod]
    [DataRow("argument")]
    [DataRow("unsupported")]
    [DataRow("relative-too-long")]
    [DataRow("parent-segment-too-long")]
    [DataRow("dot-segment-too-long")]
    public void Framework_ResolutionFailurePreservesUnsafeToPrefixInput(string failure)
    {
        string path = failure switch
        {
            "parent-segment-too-long" => @"C:\reports\..\trace.log",
            "dot-segment-too-long" => @"C:\reports\.\trace.log",
            _ => @"reports\trace.log",
        };
        Exception original = failure switch
        {
            "argument" => new ArgumentException("malformed"),
            "unsupported" => new NotSupportedException("unsupported"),
            _ => new PathTooLongException("legacy resolver cannot normalize"),
        };
        int calls = 0;

        string actual = TrxLongPathHelper.GetPathForFileSystemAccess(path, input =>
        {
            calls++;
            Assert.AreEqual(path, input);
            throw original;
        });

        Assert.AreEqual(path, actual);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    [DataRow(259, false)]
    [DataRow(260, false)]
    [DataRow(259, true)]
    [DataRow(260, true)]
    public void Framework_ResolvedLengthControlsExactPrefixBoundary(int resolvedLength, bool unc)
    {
        const string Input = "report.trx";
        string root = unc ? @"\\server\share\" : @"C:\";
        string resolved = root + new string('x', resolvedLength - root.Length);
        string expected = resolvedLength == 259 ? Input : unc ? @"\\?\UNC\" + resolved[2..] : @"\\?\" + resolved;
        int calls = 0;

        string actual = TrxLongPathHelper.GetPathForFileSystemAccess(Input, input =>
        {
            calls++;
            Assert.AreEqual(Input, input);
            return resolved;
        });

        Assert.AreEqual(expected, actual);
        Assert.AreEqual(1, calls);
    }
#endif
}
