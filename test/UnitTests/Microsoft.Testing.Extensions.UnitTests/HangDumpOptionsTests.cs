// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpOptionsTests
{
    [TestMethod]
    public void IsEnabled_ReturnsTrue_WhenHangDumpOptionIsSet()
    {
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
        });

        Assert.IsTrue(HangDumpOptions.IsEnabled(commandLineOptions));
    }

    [TestMethod]
    public void IsEnabled_ReturnsFalse_WhenHangDumpOptionIsNotSet()
    {
        var commandLineOptions = new TestCommandLineOptions([]);

        Assert.IsFalse(HangDumpOptions.IsEnabled(commandLineOptions));
    }
}
