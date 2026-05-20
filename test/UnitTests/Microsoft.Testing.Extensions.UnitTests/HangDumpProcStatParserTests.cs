// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpProcStatParserTests
{
    // The implementation's InvalidProcessId constant is private, so we reference the literal -1 here.
    private const int InvalidProcessId = -1;

    [TestMethod]
    [DataRow("93 (bash) S 92 93 2 4294967295 0 0 0 0", 92)]
    [DataRow("1234 (Web Content) S 4567 1234 1234 0 -1 4194304 0 0", 4567)]
    [DataRow("42 (My (App)) R 100 42 42 0 -1 4194304 0 0", 100)]
    [DataRow("5000 (chrome_helper_re) S 4999 5000 5000 0 -1 4194304 0 0", 4999)]
    [DataRow("1 (systemd) S 0 1 1 0 -1 4194560 0 0", 0)]
    public void ParseParentPidFromProcStat_ValidInput_ReturnsParentPid(string stat, int expected)
    {
        int actual = IProcessExtensions.ParseParentPidFromProcStat(stat);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParseParentPidFromProcStat_EmptyString_ReturnsInvalidProcessId()
        => Assert.AreEqual(InvalidProcessId, IProcessExtensions.ParseParentPidFromProcStat(string.Empty));

    [TestMethod]
    public void ParseParentPidFromProcStat_NoClosingParen_ReturnsInvalidProcessId()
        => Assert.AreEqual(InvalidProcessId, IProcessExtensions.ParseParentPidFromProcStat("93 (bash S 92 93 2"));

    [TestMethod]
    public void ParseParentPidFromProcStat_ClosingParenAtEnd_ReturnsInvalidProcessId()
        => Assert.AreEqual(InvalidProcessId, IProcessExtensions.ParseParentPidFromProcStat("93 (bash)"));

    [TestMethod]
    public void ParseParentPidFromProcStat_NonNumericPpid_ReturnsInvalidProcessId()
        => Assert.AreEqual(InvalidProcessId, IProcessExtensions.ParseParentPidFromProcStat("1 (a) S notanumber 1 1 0 -1 0 0 0"));

    [TestMethod]
    public void ParseParentPidFromProcStat_NotEnoughFieldsAfterComm_ReturnsInvalidProcessId()
        => Assert.AreEqual(InvalidProcessId, IProcessExtensions.ParseParentPidFromProcStat("1 (a) S"));
}
