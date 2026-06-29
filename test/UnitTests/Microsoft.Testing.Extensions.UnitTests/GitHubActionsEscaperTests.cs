// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsEscaperTests
{
    [TestMethod]
    public void EscapeData_EscapesPercentAndNewlines()
        => Assert.AreEqual("a%25b%0Ac%0Dd", GitHubActionsEscaper.EscapeData("a%b\nc\rd"));

    [TestMethod]
    public void EscapeData_LeavesPlainTextUntouched()
        => Assert.AreEqual("Tests: MSTest.UnitTests (net9.0)", GitHubActionsEscaper.EscapeData("Tests: MSTest.UnitTests (net9.0)"));

    [TestMethod]
    public void EscapeData_ReturnsEmptyForEmpty()
        => Assert.AreEqual(string.Empty, GitHubActionsEscaper.EscapeData(string.Empty));
}
