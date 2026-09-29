// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

// The same parser source is linked into the classic UWP bootstrap in MSTest.TestAdapter, so these
// cases also cover how that bootstrap resolves the connect-back handshake options.
[TestClass]
public sealed class PackagedAppCommandLineOptionParserTests
{
    [TestMethod]
    public void TryGetOptionValue_ReturnsNextArgument_WhenOptionIsSeparate()
        => Assert.AreEqual("9876", CommandLineOptionParser.TryGetOptionValue(["--internal-testhostcontroller-pid", "9876"], "--internal-testhostcontroller-pid"));

    [DataRow("=")]
    [DataRow(":")]
    [TestMethod]
    public void TryGetOptionValue_ReturnsInlineValue_ForControllerPid(string separator)
        => Assert.AreEqual("9876", CommandLineOptionParser.TryGetOptionValue([$"--internal-testhostcontroller-pid{separator}9876"], "--internal-testhostcontroller-pid"));

    [DataRow("=")]
    [DataRow(":")]
    [TestMethod]
    public void TryGetOptionValue_ReturnsInlineValue_ForRetryPipeName(string separator)
        => Assert.AreEqual(
            @"LOCAL\testingplatform.pipe.retry",
            CommandLineOptionParser.TryGetOptionValue([$@"--internal-retry-pipename{separator}LOCAL\testingplatform.pipe.retry"], "--internal-retry-pipename"));

    [TestMethod]
    public void TryGetOptionValue_ReturnsNull_WhenOptionIsLastWithoutValue()
        => Assert.IsNull(CommandLineOptionParser.TryGetOptionValue(["--internal-retry-pipename"], "--internal-retry-pipename"));

    [TestMethod]
    public void TryGetOptionValue_IgnoresOptionsSharingThePrefix()
        => Assert.IsNull(CommandLineOptionParser.TryGetOptionValue(["--internal-testhostcontroller-pidx", "9876"], "--internal-testhostcontroller-pid"));

    [TestMethod]
    public void TryGetInlineOptionValue_ReturnsFalse_WhenOptionOnlySharesThePrefix()
    {
        Assert.IsFalse(CommandLineOptionParser.TryGetInlineOptionValue("--internal-testhostcontroller-pidx=9876", "--internal-testhostcontroller-pid", out string? value));
        Assert.IsNull(value);
    }
}

#endif
