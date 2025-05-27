// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Tools;
using Moq;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public class ToolCommandLineOptionsProviderCacheTests
{
    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_CachesResults()
    {
        // Arrange
        var mockProvider = new Mock<IToolCommandLineOptionsProvider>();
        mockProvider.Setup(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());

        var cache = new ToolCommandLineOptionsProviderCache(mockProvider.Object);
        var option = new CommandLineOption("test-option", "Test option description", ArgumentArity.ExactlyOne, false);
        var arguments = new[] { "arg1" };

        // Act
        await cache.ValidateOptionArgumentsAsync(option, arguments);
        await cache.ValidateOptionArgumentsAsync(option, arguments);

        // Assert - should only call underlying provider once due to caching
        mockProvider.Verify(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()), Times.Once);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_DifferentOptions_NoCache()
    {
        // Arrange
        var mockProvider = new Mock<IToolCommandLineOptionsProvider>();
        mockProvider.Setup(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());

        var cache = new ToolCommandLineOptionsProviderCache(mockProvider.Object);
        var option1 = new CommandLineOption("option1", "Option1 description", ArgumentArity.ExactlyOne, false);
        var option2 = new CommandLineOption("option2", "Option2 description", ArgumentArity.ExactlyOne, false);
        var arguments = new[] { "arg1" };

        // Act
        await cache.ValidateOptionArgumentsAsync(option1, arguments);
        await cache.ValidateOptionArgumentsAsync(option2, arguments);

        // Assert - should call underlying provider twice for different options
        mockProvider.Verify(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_DifferentArguments_NoCache()
    {
        // Arrange
        var mockProvider = new Mock<IToolCommandLineOptionsProvider>();
        mockProvider.Setup(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());

        var cache = new ToolCommandLineOptionsProviderCache(mockProvider.Object);
        var option = new CommandLineOption("option", "Option description", ArgumentArity.ExactlyOne, false);
        var arguments1 = new[] { "arg1" };
        var arguments2 = new[] { "arg2" };

        // Act
        await cache.ValidateOptionArgumentsAsync(option, arguments1);
        await cache.ValidateOptionArgumentsAsync(option, arguments2);

        // Assert - should call underlying provider twice for different arguments
        mockProvider.Verify(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_SameOptionAndArguments_UsesCache()
    {
        // Arrange
        var mockProvider = new Mock<IToolCommandLineOptionsProvider>();
        mockProvider.Setup(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());

        var cache = new ToolCommandLineOptionsProviderCache(mockProvider.Object);
        var option = new CommandLineOption("option", "Option description", ArgumentArity.ExactlyOne, false);
        var arguments = new[] { "arg1", "arg2" };

        // Act - call multiple times with same option and arguments
        await cache.ValidateOptionArgumentsAsync(option, arguments);
        await cache.ValidateOptionArgumentsAsync(option, arguments);
        await cache.ValidateOptionArgumentsAsync(option, arguments);

        // Assert - should only call underlying provider once due to caching
        mockProvider.Verify(p => p.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()), Times.Once);
    }
}