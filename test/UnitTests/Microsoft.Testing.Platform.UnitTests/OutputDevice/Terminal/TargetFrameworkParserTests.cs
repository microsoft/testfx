// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class TargetFrameworkParserTests : TestBase
{
    public TargetFrameworkParserTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    // known 2 digit versions
    [Arguments(".NET Framework 4.7.0", "net47")]
    [Arguments(".NET Framework 4.8.0", "net48")]

    // known 3 digit versions
    [Arguments(".NET Framework 4.6.2", "net462")]
    [Arguments(".NET Framework 4.7.1", "net471")]
    [Arguments(".NET Framework 4.7.2", "net472")]
    [Arguments(".NET Framework 4.8.1", "net481")]

    // other
    [Arguments(".NET Framework 4.6.3", "net46")]
    [Arguments(".NET Framework 4.8.9", "net48")]
    public void ParseNETFramework(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [Arguments(".NET Core 3.1.0", "netcoreapp3.1")]
    public void ParseNETCore(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [Arguments(".NET 6.0.0", "net6.0")]
    [Arguments(".NET 8.0.0", "net8.0")]
    [Arguments(".NET 10.0.0", "net10.0")]
    public void ParseNET(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));
}
