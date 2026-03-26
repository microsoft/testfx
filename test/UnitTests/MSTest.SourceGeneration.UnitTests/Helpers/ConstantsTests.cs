// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias SourceGen;

using AwesomeAssertions;

using Constants = SourceGen::Microsoft.Testing.Framework.SourceGeneration.Constants;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests;

[TestClass]
public class ConstantsTests : TestBase
{
    [TestMethod]
    public void NewLine_IsWindowsLineReturn() => Constants.NewLine.Should().Be("\r\n");
}
