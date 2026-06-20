// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class JUnitReportEngineApiTests
{
    [TestMethod]
    public void XmlSafeText_RemainsInternalAndSanitizesInvalidCharacters()
        => Assert.AreEqual("a\uFFFDb", JUnitReportEngine.XmlSafeText("a\u0001b"));
}
