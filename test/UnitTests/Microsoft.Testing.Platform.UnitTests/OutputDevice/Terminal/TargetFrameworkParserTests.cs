// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TargetFrameworkParserTests
{
    // known 2 digit versions
    [DataRow(".NET Framework 4.7.0", "net47")]
    [DataRow(".NET Framework 4.8.0", "net48")]

    // known 3 digit versions
    [DataRow(".NET Framework 4.6.2", "net462")]
    [DataRow(".NET Framework 4.7.1", "net471")]
    [DataRow(".NET Framework 4.7.2", "net472")]
    [DataRow(".NET Framework 4.8.1", "net481")]

    // other
    [DataRow(".NET Framework 4.6.3", "net46")]
    [DataRow(".NET Framework 4.8.9", "net48")]
    [TestMethod]
    public void ParseNETFramework(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [DataRow(".NET Core 3.1.0", "netcoreapp3.1")]
    [TestMethod]
    public void ParseNETCore(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [DataRow(".NET 6.0.0", "net6.0")]
    [DataRow(".NET 8.0.0", "net8.0")]
    [DataRow(".NET 10.0.0", "net10.0")]
    [TestMethod]
    public void ParseNET(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));
}
