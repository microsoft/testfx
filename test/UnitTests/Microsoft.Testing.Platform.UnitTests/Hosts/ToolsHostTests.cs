// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Hosts;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

#pragma warning disable TPEXP // Command-line parse models are experimental.

[TestClass]
public sealed class ToolsHostTests
{
    [TestMethod]
    public void AggregateArguments_CombinesRepeatedOptionOccurrences()
    {
        CommandLineParseOption[] occurrences =
        [
            new("input", ["a.trx"]),
            new("input", ["b.trx"]),
        ];

        Assert.AreSequenceEqual(["a.trx", "b.trx"], ToolsHost.AggregateArguments(occurrences));
    }
}
