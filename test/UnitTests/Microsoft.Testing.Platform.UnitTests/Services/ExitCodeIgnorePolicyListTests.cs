// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ExitCodeIgnorePolicyListTests
{
    [TestMethod]
    [DataRow(";5", 5, 0)]
    [DataRow(";;5", 5, 0)]
    [DataRow("2;5", 5, 0)]
    [DataRow("5;", 5, 0)]
    [DataRow("5;", 2, 2)]
    [DataRow(";", 2, 2)]
    [DataRow("2;3", 5, 5)]
    public void Apply_ListWithEmptySegments_IgnoresOnlyListedCodes(string list, int exitCode, int expected)
    {
        TestCommandLineOptions options = new(new Dictionary<string, string[]> { [PlatformCommandLineProvider.IgnoreExitCodeOptionKey] = [list] });

        Assert.AreEqual(expected, ExitCodeIgnorePolicy.Apply(exitCode, options, Mock.Of<IEnvironment>()));
    }
}
