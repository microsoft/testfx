// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LLMEnvironmentDetectorTests
{
    [TestMethod]
    [DataRow("github_copilot_app_agent")]
    [DataRow("GitHub_Copilot_App_Agent")]
    [DataRow("github_copilot_vscode_agent")]
    public void IsLLMEnvironment_WhenAIAgentHoldsAKnownAgentValue_ReturnsTrue(string aiAgentValue)
        => Assert.IsTrue(CreateDetector(aiAgentValue).IsLLMEnvironment());

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("github_copilot_app")]
    [DataRow("some_other_agent")]
    public void IsLLMEnvironment_WhenAIAgentDoesNotHoldAKnownAgentValue_ReturnsFalse(string? aiAgentValue)
        => Assert.IsFalse(CreateDetector(aiAgentValue).IsLLMEnvironment());

    [TestMethod]
    [DataRow("OPENCODE", "1", true)]
    [DataRow("OPENCODE", "false", true)]
    [DataRow("OPENCODE", "", false)]
    [DataRow("OPENCODE", null, false)]
    [DataRow("OPENCODE_AI", "1", false)]
    public void IsLLMEnvironment_WhenCheckingOpenCode_ReturnsExpectedResult(string variableName, string? value, bool expected)
        => Assert.AreEqual(expected, CreateDetector(value, variableName).IsLLMEnvironment());

    private static LLMEnvironmentDetector CreateDetector(string? value, string variableName = "AI_AGENT")
    {
        Mock<IEnvironment> environment = new();
        _ = environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);
        _ = environment.Setup(x => x.GetEnvironmentVariable(variableName)).Returns(value);
        return new LLMEnvironmentDetector(environment.Object);
    }
}
