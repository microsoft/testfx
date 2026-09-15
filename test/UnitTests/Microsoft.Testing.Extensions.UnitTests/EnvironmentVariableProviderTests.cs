// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class EnvironmentVariableProviderTests
{
    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_UpdateAsync_SetsLockedPipeNameVariable()
    {
        var environmentVariables = new Mock<IEnvironmentVariables>();
        EnvironmentVariable? setVariable = null;
        environmentVariables.Setup(x => x.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(variable => setVariable = variable);

        var endpoint = new NamedPipeServerEndpoint("initial-pipe");
        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), endpoint);
        endpoint.PipeName = "expected-pipe";

        await provider.UpdateAsync(environmentVariables.Object);

        Assert.IsNotNull(setVariable);
        Assert.AreEqual(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, setVariable.Variable);
        Assert.AreEqual("expected-pipe", setVariable.Value);
        Assert.IsFalse(setVariable.IsSecret);
        Assert.IsTrue(setVariable.IsLocked);
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_ValidateTestHostEnvironmentVariablesAsync_ReturnsInvalid_WhenPipeNameValueDiffers()
    {
        var readOnlyEnvironmentVariables = new Mock<IReadOnlyEnvironmentVariables>();
        var existingVariable = new OwnedEnvironmentVariable(
            new TestExtension(),
            HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName,
            "actual-pipe",
            isSecret: false,
            isLocked: true);
        readOnlyEnvironmentVariables
            .Setup(x => x.TryGetVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, out existingVariable))
            .Returns(true);

        var endpoint = new NamedPipeServerEndpoint("initial-pipe");
        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), endpoint);
        endpoint.PipeName = "expected-pipe";

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(readOnlyEnvironmentVariables.Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, result.ErrorMessage!);
        Assert.Contains("actual-pipe", result.ErrorMessage!);
        Assert.Contains("expected-pipe", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task TrxEnvironmentVariableProvider_ValidateTestHostEnvironmentVariablesAsync_ReturnsValid_WhenVariableExistsWithDifferentValue()
    {
        var readOnlyEnvironmentVariables = new Mock<IReadOnlyEnvironmentVariables>();
        var existingVariable = new OwnedEnvironmentVariable(
            new TestExtension(),
            TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME,
            "different-value",
            isSecret: false,
            isLocked: true);
        readOnlyEnvironmentVariables
            .Setup(x => x.TryGetVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, out existingVariable))
            .Returns(true);

        var provider = new TrxEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("expected-value"));

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(readOnlyEnvironmentVariables.Object);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_ValidateTestHostEnvironmentVariablesAsync_ReturnsInvalid_WhenVariableIsMissing()
    {
        var readOnlyEnvironmentVariables = new Mock<IReadOnlyEnvironmentVariables>();
        OwnedEnvironmentVariable? missingVariable = null;
        readOnlyEnvironmentVariables
            .Setup(x => x.TryGetVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, out missingVariable))
            .Returns(false);

        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("expected-pipe"));

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(readOnlyEnvironmentVariables.Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, result.ErrorMessage!);
    }

    [TestMethod]
    public async Task TrxEnvironmentVariableProvider_ValidateTestHostEnvironmentVariablesAsync_ReturnsInvalid_WhenVariableIsMissing()
    {
        var readOnlyEnvironmentVariables = new Mock<IReadOnlyEnvironmentVariables>();
        OwnedEnvironmentVariable? missingVariable = null;
        readOnlyEnvironmentVariables
            .Setup(x => x.TryGetVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, out missingVariable))
            .Returns(false);

        var provider = new TrxEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("expected-value"));

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(readOnlyEnvironmentVariables.Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, result.ErrorMessage!);
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_ValidateTestHostEnvironmentVariablesAsync_ReturnsValid_WhenPipeNameValueMatches()
    {
        var readOnlyEnvironmentVariables = new Mock<IReadOnlyEnvironmentVariables>();
        var existingVariable = new OwnedEnvironmentVariable(
            new TestExtension(),
            HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName,
            "expected-pipe",
            isSecret: false,
            isLocked: true);
        readOnlyEnvironmentVariables
            .Setup(x => x.TryGetVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, out existingVariable))
            .Returns(true);

        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("expected-pipe"));

        ValidationResult result = await provider.ValidateTestHostEnvironmentVariablesAsync(readOnlyEnvironmentVariables.Object);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_IsEnabledAsync_ReturnsTrue_WhenHangDumpOptionIsSet()
    {
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            ["hangdump"] = [],
        });
        var provider = new HangDumpEnvironmentVariableProvider(commandLineOptions, new NamedPipeServerEndpoint("pipe"));

        Assert.IsTrue(await provider.IsEnabledAsync());
    }

    [TestMethod]
    public async Task HangDumpEnvironmentVariableProvider_IsEnabledAsync_ReturnsFalse_WhenHangDumpOptionIsNotSet()
    {
        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("pipe"));

        Assert.IsFalse(await provider.IsEnabledAsync());
    }

    [TestMethod]
    public void HangDumpEnvironmentVariableProvider_Accessors_ReturnExpectedValues()
    {
        var provider = new HangDumpEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("pipe"));

        Assert.AreEqual(nameof(HangDumpEnvironmentVariableProvider), provider.Uid);
        Assert.IsFalse(string.IsNullOrEmpty(provider.DisplayName));
        Assert.IsFalse(string.IsNullOrEmpty(provider.Description));
    }

    [TestMethod]
    public void TrxEnvironmentVariableProvider_Accessors_ReturnExpectedValues()
    {
        var provider = new TrxEnvironmentVariableProvider(new TestCommandLineOptions([]), new NamedPipeServerEndpoint("pipe"));

        Assert.AreEqual(nameof(TrxEnvironmentVariableProvider), provider.Uid);
        Assert.IsFalse(string.IsNullOrEmpty(provider.DisplayName));
        Assert.IsFalse(string.IsNullOrEmpty(provider.Description));
    }

    private sealed class TestExtension : IExtension
    {
        public string Uid => nameof(TestExtension);

        public string Version => "1.0.0";

        public string DisplayName => nameof(TestExtension);

        public string Description => nameof(TestExtension);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
