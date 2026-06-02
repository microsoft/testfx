// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class MSBuildLifecycleCallbacksTests
{
    private const string MSBuildNodeOptionKey = "internal-msbuild-node";

    [TestMethod]
    public async Task TestApplicationLifecycle_BeforeRunAsync_Throws_WhenPipeOptionIsMissing()
    {
        MSBuildTestApplicationLifecycleCallbacks sut = new(CreateConfiguration(), CreateOptionsMissingPipeName());

        InvalidOperationException exception = await ThrowingAssert.ThrowsInvalidOperationAsync(() => sut.BeforeRunAsync(CancellationToken.None));

        Assert.Contains($"missing {MSBuildNodeOptionKey}", exception.Message);
    }

    [TestMethod]
    public async Task TestApplicationLifecycle_BeforeRunAsync_Throws_WhenPipeOptionArgumentIsInvalid()
    {
        MSBuildTestApplicationLifecycleCallbacks sut = new(CreateConfiguration(), CreateOptionsWithInvalidPipeName());

        InvalidOperationException exception = await ThrowingAssert.ThrowsInvalidOperationAsync(() => sut.BeforeRunAsync(CancellationToken.None));

        Assert.Contains($"missing argument for {MSBuildNodeOptionKey}", exception.Message);
    }

    [TestMethod]
    public async Task OrchestratorLifecycle_BeforeRunAsync_Throws_WhenPipeOptionIsMissing()
    {
        MSBuildOrchestratorLifetime sut = new(CreateConfiguration(), CreateOptionsMissingPipeName());

        InvalidOperationException exception = await ThrowingAssert.ThrowsInvalidOperationAsync(() => sut.BeforeRunAsync(CancellationToken.None));

        Assert.Contains($"missing {MSBuildNodeOptionKey}", exception.Message);
    }

    [TestMethod]
    public async Task OrchestratorLifecycle_BeforeRunAsync_Throws_WhenPipeOptionArgumentIsInvalid()
    {
        MSBuildOrchestratorLifetime sut = new(CreateConfiguration(), CreateOptionsWithInvalidPipeName());

        InvalidOperationException exception = await ThrowingAssert.ThrowsInvalidOperationAsync(() => sut.BeforeRunAsync(CancellationToken.None));

        Assert.Contains($"missing argument for {MSBuildNodeOptionKey}", exception.Message);
    }

    private static IConfiguration CreateConfiguration() => Mock.Of<IConfiguration>();

    private static ICommandLineOptions CreateOptionsMissingPipeName()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? msbuildInfo = null;
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(MSBuildNodeOptionKey, out msbuildInfo))
            .Returns(false);

        return commandLineOptions.Object;
    }

    private static ICommandLineOptions CreateOptionsWithInvalidPipeName()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? msbuildInfo = [string.Empty];
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(MSBuildNodeOptionKey, out msbuildInfo))
            .Returns(true);

        return commandLineOptions.Object;
    }

    private static class ThrowingAssert
    {
        public static async Task<InvalidOperationException> ThrowsInvalidOperationAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }

            Assert.Fail("Expected InvalidOperationException to be thrown.");
            return null!;
        }
    }
}
