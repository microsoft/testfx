// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PasteArgumentsTests
{
    // Test cases mirror https://github.com/dotnet/runtime/blob/3ac6e13b2780bbaf03c62488fced60b9a76e9782/src/libraries/Common/tests/Tests/System/PasteArgumentsTests.cs
    [DataRow("app.exe arg1 arg2", "app.exe", "arg1", "arg2")]
    [DataRow("\"app name.exe\" arg1 arg2", "app name.exe", "arg1", "arg2")]
    [DataRow("app.exe \\\\ arg2", "app.exe", "\\\\", "arg2")]
    [DataRow("app.exe \"\\\"\" arg2", "app.exe", "\"", "arg2")] // literal double quotation mark character
    [DataRow("app.exe \"\\\\\\\"\" arg2", "app.exe", "\\\"", "arg2")] // 2N+1 backslashes before quote rule
    [DataRow("app.exe \"\\\\\\\\\\\"\" arg2", "app.exe", "\\\\\"", "arg2")] // 2N backslashes before quote rule
    [TestMethod]
    public void Pastes(string expected, string arg0, string arg1, string arg2)
    {
        var sb = new StringBuilder();
        PasteArguments.AppendArgument(sb, arg0);
        PasteArguments.AppendArgument(sb, arg1);
        PasteArguments.AppendArgument(sb, arg2);
        Assert.AreEqual(expected, sb.ToString());
    }
}
