// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class MSBuildLifecycleCallbacksTests
{
    private const string MSBuildNodeOptionKey = "internal-msbuild-node";

    [TestMethod]
    public void GetCommandLineOptions_InternalMSBuildNodeOption_IsHiddenAndBuiltIn()
    {
        MSBuildCommandLineProvider provider = new();

        CommandLineOption option = Assert.ContainsSingle(provider.GetCommandLineOptions());

        Assert.AreEqual("MSBuildCommandLineProvider", provider.Uid);
        Assert.AreEqual(MSBuildNodeOptionKey, option.Name);
        Assert.IsTrue(option.IsHidden);
        bool isBuiltIn = (bool)typeof(CommandLineOption)
            .GetProperty("IsBuiltIn", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(option)!;
        Assert.IsTrue(isBuiltIn);
    }

    [TestMethod]
    public void TimeoutHelper_DefaultsAreInitializedAndConsistent()
    {
        Type timeoutType = typeof(MSBuildCommandLineProvider).Assembly.GetType(
            "Microsoft.Testing.Platform.Helpers.TimeoutHelper",
            throwOnError: true)!;
        var timeout = (TimeSpan)timeoutType.GetProperty("DefaultHangTimeSpanTimeout")!.GetValue(null)!;
        double timeoutSeconds = (double)timeoutType.GetProperty("DefaultHangTimeoutSeconds")!.GetValue(null)!;

        Assert.IsGreaterThan(TimeSpan.Zero, timeout);
        Assert.AreEqual(timeout.TotalSeconds, timeoutSeconds);
    }

    [DataRow("2", "Seconds", 2d)]
    [DataRow("2m", "Seconds", 120d)]
    [TestMethod]
    public void TimeSpanParser_TryParse_UsesProvidedDefaultUnitOrExplicitSuffix(
        string input,
        string defaultUnitName,
        double expectedSeconds)
    {
        Assembly extensionAssembly = typeof(MSBuildCommandLineProvider).Assembly;
        Type parserType = extensionAssembly.GetType(
            "Microsoft.Testing.Platform.Helpers.TimeSpanParser",
            throwOnError: true)!;
        Type defaultUnitType = extensionAssembly.GetType(
            "Microsoft.Testing.Platform.Helpers.TimeSpanDefaultUnit",
            throwOnError: true)!;
        MethodInfo tryParse = parserType
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method => method.Name == "TryParse" && method.GetParameters().Length == 3);
        object defaultUnit = Enum.Parse(defaultUnitType, defaultUnitName);
        object?[] arguments = [input, defaultUnit, TimeSpan.Zero];

        bool success = (bool)tryParse.Invoke(null, arguments)!;
        var result = (TimeSpan)arguments[2]!;

        Assert.IsTrue(success);
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), result);
    }

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
