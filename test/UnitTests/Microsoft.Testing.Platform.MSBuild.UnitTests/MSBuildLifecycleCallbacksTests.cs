// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
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

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task Lifecycle_IsEnabledAndAfterRun_DependOnlyOnOptionAndDoNotOpenPipe(bool testApplication, bool optionSet)
    {
        Mock<ICommandLineOptions> options = new(MockBehavior.Strict);
        options.Setup(x => x.IsOptionSet(MSBuildNodeOptionKey)).Returns(optionSet);
        using MSBuildTestApplicationLifecycleCallbacks host = new(CreateConfiguration(), options.Object);
        MSBuildLifecycleCallbacksBase lifecycle = testApplication
            ? host
            : new MSBuildOrchestratorLifetime(CreateConfiguration(), options.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.AreEqual(optionSet, await lifecycle.IsEnabledAsync());
        await lifecycle.AfterRunAsync(exitCode: 1, cancellation.Token);
        await lifecycle.AfterRunAsync(exitCode: 0, CancellationToken.None);

        Assert.IsNull(host.PipeClient);
        options.VerifyAll();
        options.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(true, "null")]
    [DataRow(true, "empty")]
    [DataRow(true, "null-element")]
    [DataRow(true, "multiple")]
    [DataRow(false, "null")]
    [DataRow(false, "empty")]
    [DataRow(false, "null-element")]
    [DataRow(false, "multiple")]
    public async Task Lifecycle_BeforeRunAsync_InvalidArgumentCardinality_RejectsBeforeCreatingPipe(bool testApplication, string argumentShape)
    {
        string[]? arguments = argumentShape switch
        {
            "null" => null,
            "empty" => [],
            "null-element" => [null!],
            "multiple" => ["first-pipe", "second-pipe"],
            _ => throw new ArgumentOutOfRangeException(nameof(argumentShape)),
        };
        ICommandLineOptions options = CreateOptions(arguments);
        using MSBuildTestApplicationLifecycleCallbacks host = new(CreateConfiguration(), options);
        Task beforeRun = testApplication
            ? host.BeforeRunAsync(CancellationToken.None)
            : new MSBuildOrchestratorLifetime(CreateConfiguration(), options).BeforeRunAsync(CancellationToken.None);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => beforeRun);

        Assert.AreEqual($"MSBuild pipe name not found in the command line, missing argument for {MSBuildNodeOptionKey}", exception.Message);
        Assert.IsNull(host.PipeClient);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Lifecycle_CreatePipeClient_RegistersExactlyTheSerializersNeededByItsRole(bool testApplication)
    {
        string pipeName = $"testfx-msbuild-unit-{Guid.NewGuid():N}";
        using MSBuildTestApplicationLifecycleCallbacks host = new(CreateConfiguration(), CreateOptions([pipeName]));
        MSBuildLifecycleCallbacksBase lifecycle = testApplication
            ? host
            : new MSBuildOrchestratorLifetime(CreateConfiguration(), CreateOptions([pipeName]));
        MethodInfo createPipe = typeof(MSBuildLifecycleCallbacksBase).GetMethod("CreatePipeClient", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // The IPC implementation is source-linked into multiple assemblies; inspect this extension's copy.
        using var client = (IDisposable)createPipe.Invoke(lifecycle, null)!;

        Assert.AreEqual(pipeName, client.GetType().GetProperty("PipeName")!.GetValue(client));
        Assert.IsFalse((bool)client.GetType().GetProperty("IsConnected")!.GetValue(client)!);
        Type registryType = typeof(MSBuildCommandLineProvider).Assembly.GetType("Microsoft.Testing.Platform.IPC.NamedPipeBase", throwOnError: true)!;
        var serializers = (IDictionary)registryType.GetField("_typeSerializer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        string[] registeredTypes = serializers.Keys.Cast<Type>().Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] expectedTypes = testApplication
            ? ["FailedTestInfoRequest", "ModuleInfoRequest", "RunSummaryInfoRequest", "VoidResponse"]
            : ["ModuleInfoRequest", "VoidResponse"];

        Assert.AreSequenceEqual(expectedTypes, registeredTypes);
        foreach (DictionaryEntry entry in serializers)
        {
            string serializerName = entry.Value!.GetType().Name;
            string expectedSerializerName = $"{((Type)entry.Key).Name}Serializer";
            Assert.AreEqual(expectedSerializerName, serializerName);
        }

        Assert.IsNull(host.PipeClient, "Creating a client alone must not publish it as an initialized lifecycle connection.");
    }

    [TestMethod]
    public void TestApplicationLifecycle_DisposeBeforeInitialization_IsIdempotent()
    {
        using MSBuildTestApplicationLifecycleCallbacks lifecycle = new(CreateConfiguration(), CreateOptionsMissingPipeName());

        lifecycle.Dispose();
        lifecycle.Dispose();

        Assert.IsNull(lifecycle.PipeClient);
    }

    private static IConfiguration CreateConfiguration() => Mock.Of<IConfiguration>();

    private static ICommandLineOptions CreateOptions(string[]? arguments)
    {
        Mock<ICommandLineOptions> options = new(MockBehavior.Strict);
        options.Setup(x => x.TryGetOptionArgumentList(MSBuildNodeOptionKey, out arguments)).Returns(true);
        return options.Object;
    }

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
