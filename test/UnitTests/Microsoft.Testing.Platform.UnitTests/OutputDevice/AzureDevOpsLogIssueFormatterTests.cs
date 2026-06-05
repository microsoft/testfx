// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class AzureDevOpsLogIssueFormatterTests
{
    [TestMethod]
    public void FormatLogIssue_EmitsLogIssueCommandWithGivenSeverity()
        // Trim '#' so the assertion failure (if it fires) does not itself contain a ##vso
        // prefix that an Azure DevOps agent watching this test output could mistakenly act on.
        => Assert.AreEqual(
            "vso[task.logissue type=error]boom",
            AzureDevOpsLogIssueFormatter.FormatLogIssue("error", "boom").TrimStart('#'));

    [TestMethod]
    public void FormatLogIssue_EscapesSemicolonsCarriageReturnsNewlinesPercentAndCloseBracket()
        => Assert.AreEqual(
            "vso[task.logissue type=warning]a%3Bb%0Dc%0Ad%25e%5Df",
            AzureDevOpsLogIssueFormatter.FormatLogIssue("warning", "a;b\rc\nd%e]f").TrimStart('#'));

    [TestMethod]
    public void Escape_PercentIsEscapedFirstSoOtherSequencesAreNotDoubleEncoded()
        // If `;` were escaped before `%`, "%3B" in input would round-trip back to ";" on the
        // agent side. We must produce "%253B" so the agent sees a literal "%3B" in the message.
        => Assert.AreEqual("%253B%3B%0A%5D", AzureDevOpsLogIssueFormatter.Escape("%3B;\n]"));

    [TestMethod]
    public void FormatLogIssue_LeavesPlainAsciiUnchanged()
        => Assert.AreEqual(
            "vso[task.logissue type=error]plain message",
            AzureDevOpsLogIssueFormatter.FormatLogIssue("error", "plain message").TrimStart('#'));

    [TestMethod]
    public void Escape_EmptyStringRoundTrips()
        => Assert.AreEqual(string.Empty, AzureDevOpsLogIssueFormatter.Escape(string.Empty));

    [TestMethod]
    public void Escape_NoEscapableCharsReturnsInputInstance()
    {
        // Defensive check that we are not allocating a new StringBuilder when nothing needs escaping.
        string input = "no special chars";
        string result = AzureDevOpsLogIssueFormatter.Escape(input);
        Assert.AreSame(input, result);
    }

    [TestMethod]
    public void IsAzureDevOpsEnvironment_ReturnsTrueWhenTfBuildIsTrue()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_AZDO_OUTPUT")).Returns((string?)null);

        Assert.IsTrue(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }

    [TestMethod]
    public void IsAzureDevOpsEnvironment_IsCaseInsensitiveForTfBuild()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("TRUE");

        Assert.IsTrue(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }

    [TestMethod]
    public void IsAzureDevOpsEnvironment_ReturnsFalseWhenTfBuildIsAbsent()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);

        Assert.IsFalse(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }

    [TestMethod]
    public void IsAzureDevOpsEnvironment_ReturnsFalseWhenTfBuildIsFalse()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("false");

        Assert.IsFalse(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }

    [DataRow("off")]
    [DataRow("OFF")]
    [DataRow("false")]
    [DataRow("False")]
    [DataRow("0")]
    [TestMethod]
    public void IsAzureDevOpsEnvironment_ReturnsFalseWhenOptOutEnvironmentVariableIsSet(string optOutValue)
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_AZDO_OUTPUT")).Returns(optOutValue);

        Assert.IsFalse(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }

    [TestMethod]
    public void IsAzureDevOpsEnvironment_IgnoresUnknownOptOutValue()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(e => e.GetEnvironmentVariable("TESTINGPLATFORM_AZDO_OUTPUT")).Returns("on");

        Assert.IsTrue(AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(environment.Object));
    }
}
