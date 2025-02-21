// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests;

[TestClass]
public sealed class InlineTestMethodArgumentsInfoTests : TestBase
{
    [DataRow("a", "a")]
    [DataRow("a, b", "a, b")]
    [DataRow("\"ok\"", "\\\"ok\\\"")]
    [DataRow("\"", "\\\"")]
    [DataRow("\\", "\\")]
    [DataRow("\\\\", "\\\\")]
    [DataRow("\\\"", "\\\"")]
    [TestMethod]
    public void EscapeArgument_ProducesCorrectString(string value, string expectedEscapedValue)
    {
        // Arrange
        StringBuilder stringBuilder = new();

        // Act
        DataRowTestMethodArgumentsInfo.EscapeArgument(value, stringBuilder);

        // Assert
        Assert.AreEqual(expectedEscapedValue, stringBuilder.ToString());
    }
}
